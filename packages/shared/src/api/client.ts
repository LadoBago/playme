import type { ZodType } from 'zod';

import { ProblemDetailsSchema, RoomSchema } from './schemas';
import type { CreateRoomRequest, JoinRoomRequestBody, RoomDto } from './types';

/**
 * Typed result wrapper. The API surfaces an i18n key on failure
 * (docs/observability-and-i18n.md); the client maps it to a localized
 * message via t().
 */
export type ApiResult<T> =
  | { ok: true; value: T }
  | { ok: false; status: number; code: string; detail?: string };

export interface PlaymeClientOptions {
  /**
   * Base URL prefix for requests. Empty string for same-origin (the dev
   * default, since Next.js rewrites proxy /api/* to the API). In production
   * SSR this is set to the absolute API base URL.
   */
  baseUrl?: string;
  /**
   * Fetch override (for SSR with credentials forwarding, tests, etc.).
   * Defaults to the global fetch.
   */
  fetch?: typeof fetch;
}

export class PlaymeClient {
  private readonly _baseUrl: string;
  private readonly _fetch: typeof fetch;

  constructor(options: PlaymeClientOptions = {}) {
    this._baseUrl = options.baseUrl ?? '';
    this._fetch = options.fetch ?? globalThis.fetch.bind(globalThis);
  }

  createRoom(body: CreateRoomRequest, init: RequestInit = {}): Promise<ApiResult<RoomDto>> {
    return this._post<RoomDto>('/api/rooms', body, RoomSchema, init);
  }

  joinRoom(
    code: string,
    body: JoinRoomRequestBody,
    init: RequestInit = {},
  ): Promise<ApiResult<RoomDto>> {
    return this._post<RoomDto>(
      `/api/rooms/${encodeURIComponent(code)}/join`,
      body,
      RoomSchema,
      init,
    );
  }

  async getRoom(code: string, init: RequestInit = {}): Promise<ApiResult<RoomDto>> {
    const res = await this._fetch(`${this._baseUrl}/api/rooms/${encodeURIComponent(code)}`, {
      ...init,
      credentials: 'include',
    });
    return this._readResponse<RoomDto>(res, RoomSchema);
  }

  private async _post<T>(
    path: string,
    body: unknown,
    schema: ZodType<unknown>,
    init: RequestInit,
  ): Promise<ApiResult<T>> {
    const res = await this._fetch(`${this._baseUrl}${path}`, {
      ...init,
      method: 'POST',
      credentials: 'include',
      headers: { ...(init.headers ?? {}), 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    });
    return this._readResponse<T>(res, schema);
  }

  /**
   * Read and Zod-validate both success and failure response bodies. A success
   * body that doesn't match `schema` is downgraded to a typed failure (rather
   * than a thrown exception) so callers keep the existing `ApiResult` contract
   * and surface a localized error instead of crashing.
   *
   * The `as T` bridge mirrors the convention in `realtime/hub.ts`: Zod
   * infers optional fields as `string | undefined`, while DTO types use
   * `exactOptionalPropertyTypes`-style `?` properties. The compile-time
   * drift guards in `./schemas.ts` catch structural mismatches between the
   * two.
   */
  private async _readResponse<T>(
    res: Response,
    schema: ZodType<unknown>,
  ): Promise<ApiResult<T>> {
    const raw = await this._readJson(res);

    if (res.ok) {
      const parsed = schema.safeParse(raw);
      if (parsed.success) {
        return { ok: true, value: parsed.data as T };
      }
      return {
        ok: false,
        status: res.status,
        code: 'errors.invalidResponse',
      };
    }

    const problemParsed = ProblemDetailsSchema.safeParse(raw);
    const problem = problemParsed.success ? problemParsed.data : {};
    return {
      ok: false,
      status: res.status,
      code: problem.code ?? 'errors.unknown',
      ...(problem.detail !== undefined ? { detail: problem.detail } : {}),
    };
  }

  private async _readJson(res: Response): Promise<unknown> {
    try {
      return (await res.json()) as unknown;
    } catch {
      // Body wasn't JSON (e.g. empty 204, network-level proxy page). The
      // caller's safeParse will then fail and produce a typed error.
      return undefined;
    }
  }
}
