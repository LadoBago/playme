import { describe, expect, it } from 'vitest';
import type { RoomDto } from '@playme/shared';
import { describeActiveRoom } from './active-room-summary';

const t = (key: string) => key;
const tf = (key: string, params: Readonly<Record<string, string | number>>) =>
  `${key}(${Object.values(params).join(',')})`;

function room(overrides: Partial<RoomDto>): RoomDto {
  return {
    code: 'abc',
    gameId: 'checkers',
    sideSelectionMode: 'random',
    status: 'waitingForOpponent',
    host: { displayName: 'Lado', side: 'black' },
    hostConnected: false,
    challengerConnected: false,
    createdAt: '2026-11-07T00:00:00Z',
    score: { host: 0, challenger: 0, draws: 0 },
    ...overrides,
  };
}

const host = { code: 'abc', gameId: 'checkers', role: 'host' as const, savedAt: 0 };
const challenger = { ...host, role: 'challenger' as const };

describe('describeActiveRoom', () => {
  it('waiting for a friend: no opponent yet, waiting line', () => {
    expect(describeActiveRoom(room({}), host, t, tf)).toEqual(['resume.status.waiting']);
  });

  it('bot room before the socket registers: bot shown, no waiting line', () => {
    const r = room({
      challenger: { displayName: 'Bot', side: 'white', kind: 'bot' },
      botLevel: 'easy',
    });
    expect(describeActiveRoom(r, host, t, tf)).toEqual([
      'resume.opponent(match.bot · configure.botLevel.easy)',
    ]);
  });

  it('setting up: opponent plus the setup line', () => {
    const r = room({ status: 'settingUp', challenger: { displayName: 'Nino', side: 'white' } });
    expect(describeActiveRoom(r, host, t, tf)).toEqual([
      'resume.opponent(Nino)',
      'resume.status.settingUp',
    ]);
  });

  it('in progress: your turn when the side to move is the stored seat', () => {
    const r = room({
      status: 'inProgress',
      challenger: { displayName: 'Nino', side: 'white' },
      currentMatch: {
        gameId: 'checkers',
        sideToMove: 'white',
        moveCount: 3,
        state: '{}',
        clock: {
          hostMs: 60_000,
          challengerMs: 60_000,
          activePlayer: 'challenger',
          lastTickAt: '2026-11-07T00:00:00Z',
          serverNowAt: '2026-11-07T00:00:00Z',
        },
      },
    });
    expect(describeActiveRoom(r, challenger, t, tf)).toEqual([
      'resume.opponent(Lado)',
      'match.yourTurn',
    ]);
    expect(describeActiveRoom(r, host, t, tf)).toEqual([
      'resume.opponent(Nino)',
      'match.opponentTurn',
    ]);
  });
});
