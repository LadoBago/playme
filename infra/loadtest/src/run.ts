// Basic concurrent-rooms load test for the PlayMe API + SignalR backplane.
// See docs/loadtest.md for the runbook + how to interpret the output.
//
// Spawns N independent room scenarios in parallel. Each scenario plays a
// random TicTacToe 3x3 game end-to-end: host creates a room, challenger
// joins via HTTP, both connect SignalR and dispatch random moves until
// the server emits MatchEnded. Per-IP rate limits (10 create/min, 5
// join/min) cap how fast we can start new scenarios from a single
// machine — the script paces room starts at --ramp-per-min and lets
// the active rooms run concurrently from there.

import * as signalR from '@microsoft/signalr';
import { parseArgs } from 'node:util';
import { setTimeout as sleep } from 'node:timers/promises';
import { performance } from 'node:perf_hooks';

// ---------- Types (minimal, inlined to avoid workspace dep tangling) -------

interface RoomDto {
  code: string;
  status: 'waitingForOpponent' | 'inProgress' | 'ended' | 'awaitingRematch' | 'closed' | 'expired';
  currentMatch?: {
    clock?: { activePlayer?: 'host' | 'challenger' };
  };
}

interface RoomSessionDto {
  role: 'host' | 'challenger';
  room: RoomDto;
}

interface MatchEndedPayload {
  room: RoomDto;
}

interface MoveAcceptedPayload {
  room: RoomDto;
}

// ---------- Metrics ---------------------------------------------------------

interface Summary {
  n: number;
  p50: number;
  p95: number;
  p99: number;
  max: number;
}

class Metric {
  private samples: number[] = [];
  add(ms: number): void {
    this.samples.push(ms);
  }
  summary(): Summary {
    const n = this.samples.length;
    if (n === 0) return { n: 0, p50: 0, p95: 0, p99: 0, max: 0 };
    const sorted = [...this.samples].sort((a, b) => a - b);
    const at = (q: number): number => sorted[Math.min(n - 1, Math.floor(n * q))] ?? 0;
    return { n, p50: at(0.5), p95: at(0.95), p99: at(0.99), max: sorted[n - 1] ?? 0 };
  }
}

const metrics = {
  createRoom: new Metric(),
  joinRoomHttp: new Metric(),
  signalrStart: new Metric(),
  signalrJoinRoom: new Metric(),
  submitMove: new Metric(),
  totalRoomMs: new Metric(),
};

const errorCounts = new Map<string, number>();
function recordError(phase: string, err: unknown): void {
  const key = `${phase}: ${err instanceof Error ? err.message : String(err)}`;
  errorCounts.set(key, (errorCounts.get(key) ?? 0) + 1);
}

// ---------- Cookie jar (per scenario actor) --------------------------------

// The session cookie travels via Set-Cookie on the HTTP join/create
// response and must be sent back on the SignalR negotiation request.
// Tiny hand-rolled jar — no need for tough-cookie for this scope.
class CookieJar {
  private cookies = new Map<string, string>();

  absorb(response: Response): void {
    // node-fetch returns `set-cookie` as a single concatenated header
    // when accessed via .get(); .getSetCookie() (Node 22+) returns the
    // array we want, with one cookie per element.
    const headers = response.headers as Headers & { getSetCookie?: () => string[] };
    const setCookies = headers.getSetCookie?.() ?? [];
    for (const raw of setCookies) {
      const [pair] = raw.split(';');
      if (!pair) continue;
      const eq = pair.indexOf('=');
      if (eq <= 0) continue;
      const name = pair.slice(0, eq).trim();
      const value = pair.slice(eq + 1).trim();
      this.cookies.set(name, value);
    }
  }

  header(): string {
    return [...this.cookies.entries()].map(([k, v]) => `${k}=${v}`).join('; ');
  }
}

// ---------- HTTP + SignalR helpers -----------------------------------------

async function timed<T>(metric: Metric, work: () => Promise<T>): Promise<T> {
  const start = performance.now();
  try {
    return await work();
  } finally {
    metric.add(performance.now() - start);
  }
}

async function createRoom(target: string, jar: CookieJar, displayName: string): Promise<string> {
  return timed(metrics.createRoom, async () => {
    const res = await fetch(`${target}/api/rooms`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({
        hostDisplayName: displayName,
        gameId: 'tictactoe',
        sideSelectionMode: 'hostPicksSpecific',
        hostSide: 'x',
        // Sprint 9 PR1b: unified tictactoe module requires gameOptions.
        // Smallest 3×3 board keeps move volume comparable to the original
        // Sprint 7 baseline capture in docs/loadtest.md §7.
        gameOptions: { boardSize: 3 },
      }),
    });
    if (!res.ok) {
      throw new Error(`POST /api/rooms → ${res.status} ${await safeText(res)}`);
    }
    jar.absorb(res);
    const body = (await res.json()) as RoomDto;
    return body.code;
  });
}

async function joinRoomHttp(
  target: string,
  jar: CookieJar,
  code: string,
  displayName: string,
): Promise<void> {
  await timed(metrics.joinRoomHttp, async () => {
    const res = await fetch(`${target}/api/rooms/${code}/join`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ displayName }),
    });
    if (!res.ok) {
      throw new Error(`POST /api/rooms/${code}/join → ${res.status} ${await safeText(res)}`);
    }
    jar.absorb(res);
  });
}

async function safeText(res: Response): Promise<string> {
  try {
    const txt = await res.text();
    return txt.slice(0, 200);
  } catch {
    return '';
  }
}

function buildHub(target: string, jar: CookieJar): signalR.HubConnection {
  // Node SignalR client: Cookie header rides on both the negotiate
  // (XHR) and the upgrade (WS) requests when set here. The library's
  // own withCredentials option is browser-only.
  const cookieHeader = jar.header();
  return new signalR.HubConnectionBuilder()
    .withUrl(`${target}/hubs/room`, {
      headers: cookieHeader ? { Cookie: cookieHeader } : {},
      // Restrict to WebSockets so we don't silently fall back to long-
      // polling under load and skew results away from the path the
      // backplane actually exercises in production. Negotiation still
      // runs so the session cookie can travel on the negotiate request.
      transport: signalR.HttpTransportType.WebSockets,
    })
    .configureLogging(signalR.LogLevel.Warning)
    .build();
}

// ---------- Per-room scenario ---------------------------------------------

interface RoomOutcome {
  ok: boolean;
  moves: number;
  durationMs: number;
}

async function runOneRoom(roomId: number, target: string): Promise<RoomOutcome> {
  const start = performance.now();
  const hostJar = new CookieJar();
  const challengerJar = new CookieJar();

  let hostHub: signalR.HubConnection | null = null;
  let challengerHub: signalR.HubConnection | null = null;
  let moveCount = 0;

  try {
    // ---- Phase A: HTTP create + join -------------------------------------
    const code = await createRoom(target, hostJar, `host-${roomId}`);
    await joinRoomHttp(target, challengerJar, code, `chal-${roomId}`);

    // ---- Phase B: SignalR connect + JoinRoom (both sides) ----------------
    hostHub = buildHub(target, hostJar);
    challengerHub = buildHub(target, challengerJar);

    await Promise.all([
      timed(metrics.signalrStart, () => hostHub!.start()),
      timed(metrics.signalrStart, () => challengerHub!.start()),
    ]);

    // The two hub.JoinRoom calls drive the WaitingForOpponent → InProgress
    // transition once both land. Whichever is second triggers the server-
    // side MatchStarted broadcast. `gameOver` lets the driver loop exit
    // cleanly the moment MatchEnded fires — otherwise an already-dispatched
    // SubmitMove would race the finally-block hub teardown and surface
    // as a "Invocation canceled" error per room.
    let gameOver = false;
    const ended = new Promise<RoomDto>((resolve, reject) => {
      const onEnded = (payload: MatchEndedPayload): void => {
        gameOver = true;
        resolve(payload.room);
      };
      hostHub!.on('MatchEnded', onEnded);
      challengerHub!.on('MatchEnded', onEnded);
      hostHub!.onclose((err) => err && reject(err));
      challengerHub!.onclose((err) => err && reject(err));
    });

    const [hostSession, challengerSession] = await Promise.all([
      timed(metrics.signalrJoinRoom, () =>
        hostHub!.invoke<RoomSessionDto>('JoinRoom', code),
      ),
      timed(metrics.signalrJoinRoom, () =>
        challengerHub!.invoke<RoomSessionDto>('JoinRoom', code),
      ),
    ]);

    // ---- Phase C: play random moves until MatchEnded ---------------------
    // Track occupied cells locally — the server is authoritative, but we
    // only need to avoid resubmitting an occupied cell ourselves.
    const occupied = new Set<number>();
    let activeRoom = challengerSession.room.status === 'inProgress'
      ? challengerSession.room
      : hostSession.room;

    // MoveAccepted updates the active room snapshot for the move-driver
    // loop. Both clients listen; the driver below decides which side
    // submits next based on the latest active player.
    const onMoveAccepted = (payload: MoveAcceptedPayload): void => {
      activeRoom = payload.room;
    };
    hostHub.on('MoveAccepted', onMoveAccepted);
    challengerHub.on('MoveAccepted', onMoveAccepted);

    // The server broadcasts MatchStarted on JoinRoom-second; we already
    // have the room snapshot from the JoinRoom invoke response, so we
    // don't need to react. Register no-op handlers anyway to silence
    // the SignalR client's per-broadcast "No client method with the name
    // 'matchstarted' found" warning that otherwise floods the log.
    const noop = (): void => undefined;
    hostHub.on('MatchStarted', noop);
    challengerHub.on('MatchStarted', noop);

    const driver = (async (): Promise<void> => {
      // Cap iterations defensively in case the server stops emitting —
      // a TicTacToe board can't take more than 9 moves.
      for (let i = 0; i < 12; i++) {
        if (gameOver || activeRoom.status !== 'inProgress') return;
        const active = activeRoom.currentMatch?.clock?.activePlayer;
        if (!active) return;
        const hub = active === 'host' ? hostHub : challengerHub;
        const cell = pickFreeCell(occupied);
        if (cell === null) return;
        occupied.add(cell);
        try {
          await timed(metrics.submitMove, () =>
            hub.invoke('SubmitMove', { payload: { cell } }),
          );
          moveCount += 1;
        } catch (err) {
          // The server may reject the move if it sees a different active
          // player than we computed — race between activeRoom snapshots.
          // Drop the cell from our local occupied set and retry the
          // outer loop after a brief settle.
          occupied.delete(cell);
          recordError('submitMove', err);
          await sleep(50);
        }
        // Stay below the 60-move/min per-session limit. ~1.2s spacing
        // gives ample headroom.
        await sleep(150);
      }
    })();

    await Promise.race([
      ended,
      sleep(60_000).then(() => {
        throw new Error('room scenario timed out waiting for MatchEnded');
      }),
    ]);

    // MatchEnded fired. The driver may have a SubmitMove invoke in flight
    // for the move that *caused* the end — tearing down the hub now would
    // cancel that pending response. `gameOver` is already set (the ended
    // Promise's onEnded set it), so the driver's next iteration check
    // exits. Give it ~2s to drain its current invoke, then proceed.
    await Promise.race([
      driver.catch(() => undefined),
      sleep(2_000),
    ]);

    return { ok: true, moves: moveCount, durationMs: performance.now() - start };
  } catch (err) {
    recordError('scenario', err);
    return { ok: false, moves: moveCount, durationMs: performance.now() - start };
  } finally {
    await silentStop(hostHub);
    await silentStop(challengerHub);
  }
}

function pickFreeCell(occupied: Set<number>): number | null {
  const free: number[] = [];
  for (let i = 0; i < 9; i++) {
    if (!occupied.has(i)) free.push(i);
  }
  if (free.length === 0) return null;
  return free[Math.floor(Math.random() * free.length)] ?? null;
}

async function silentStop(hub: signalR.HubConnection | null): Promise<void> {
  if (!hub) return;
  try {
    await hub.stop();
  } catch {
    // hub already disposed by the server (MatchEnded → server keeps the
    // connection but our flow tears it down anyway). Nothing to surface.
  }
}

// ---------- Main orchestrator ---------------------------------------------

interface Args {
  target: string;
  rooms: number;
  rampPerMin: number;
}

function parseCliArgs(): Args {
  // pnpm's `start --` passthrough leaves a literal `--` in argv, which
  // parseArgs treats as a "stop parsing options" sentinel — every flag
  // after it becomes a positional and silently ignored. Strip it so
  // `--rooms 5` keeps being parsed as an option.
  const rawArgs = process.argv.slice(2).filter((a) => a !== '--');
  const { values } = parseArgs({
    args: rawArgs,
    options: {
      target: { type: 'string', default: 'http://localhost:5000' },
      rooms: { type: 'string', default: '50' },
      'ramp-per-min': { type: 'string', default: '4' },
    },
    allowPositionals: true,
  });
  const rooms = Number.parseInt(values.rooms ?? '50', 10);
  const rampPerMin = Number.parseInt(values['ramp-per-min'] ?? '4', 10);
  return {
    target: values.target ?? 'http://localhost:5000',
    rooms: Number.isFinite(rooms) && rooms > 0 ? rooms : 50,
    rampPerMin: Number.isFinite(rampPerMin) && rampPerMin > 0 ? rampPerMin : 4,
  };
}

function formatMs(ms: number): string {
  if (ms < 1) return `${ms.toFixed(2)}ms`;
  if (ms < 1000) return `${ms.toFixed(0)}ms`;
  return `${(ms / 1000).toFixed(2)}s`;
}

function printReport(args: Args, wallClockMs: number, outcomes: RoomOutcome[]): void {
  const ok = outcomes.filter((o) => o.ok).length;
  const failed = outcomes.length - ok;
  const totalMoves = outcomes.reduce((sum, o) => sum + o.moves, 0);

  console.log('\n────── Load test summary ──────');
  console.log(`Target:          ${args.target}`);
  console.log(`Rooms requested: ${args.rooms}  (ramp ${args.rampPerMin}/min)`);
  console.log(`Rooms completed: ${ok} ok, ${failed} failed`);
  console.log(`Total moves:     ${totalMoves}`);
  console.log(`Wall clock:      ${formatMs(wallClockMs)}`);
  console.log('');

  const fmt = (label: string, m: Metric): void => {
    const s = m.summary();
    if (s.n === 0) {
      console.log(`  ${label.padEnd(22)}  n=0`);
      return;
    }
    console.log(
      `  ${label.padEnd(22)}  n=${String(s.n).padStart(4)}  ` +
        `p50=${formatMs(s.p50).padStart(8)}  ` +
        `p95=${formatMs(s.p95).padStart(8)}  ` +
        `p99=${formatMs(s.p99).padStart(8)}  ` +
        `max=${formatMs(s.max).padStart(8)}`,
    );
  };

  console.log('Per-operation latency:');
  fmt('createRoom (HTTP)', metrics.createRoom);
  fmt('joinRoom (HTTP)', metrics.joinRoomHttp);
  fmt('signalR start', metrics.signalrStart);
  fmt('signalR JoinRoom', metrics.signalrJoinRoom);
  fmt('submitMove', metrics.submitMove);

  if (errorCounts.size > 0) {
    console.log('\nErrors (top 10 by count):');
    const top = [...errorCounts.entries()].sort((a, b) => b[1] - a[1]).slice(0, 10);
    for (const [msg, count] of top) {
      console.log(`  ×${String(count).padStart(4)}  ${msg}`);
    }
  } else {
    console.log('\nNo errors recorded.');
  }
  console.log('───────────────────────────────\n');
}

async function main(): Promise<void> {
  const args = parseCliArgs();
  console.log(
    `Starting load test: ${args.rooms} rooms, ramp ${args.rampPerMin}/min, target ${args.target}`,
  );

  const intervalMs = 60_000 / args.rampPerMin;
  const wallStart = performance.now();
  const inflight: Promise<RoomOutcome>[] = [];
  const outcomes: RoomOutcome[] = [];

  for (let i = 0; i < args.rooms; i++) {
    const p = runOneRoom(i, args.target).then((outcome) => {
      outcomes.push(outcome);
      metrics.totalRoomMs.add(outcome.durationMs);
      const done = outcomes.length;
      if (done % 10 === 0 || done === args.rooms) {
        const okSoFar = outcomes.filter((o) => o.ok).length;
        console.log(`  progress: ${done}/${args.rooms}  (${okSoFar} ok)`);
      }
      return outcome;
    });
    inflight.push(p);
    if (i < args.rooms - 1) await sleep(intervalMs);
  }

  await Promise.all(inflight);
  printReport(args, performance.now() - wallStart, outcomes);

  const failed = outcomes.filter((o) => !o.ok).length;
  process.exit(failed > 0 ? 1 : 0);
}

await main();
