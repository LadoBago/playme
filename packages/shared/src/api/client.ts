import type {
  CreateRoomRequest,
  JoinRoomRequestBody,
  ProblemDetailsResponse,
  RoomDto,
} from './types';

/**
 * Typed result wrapper. The API surfaces an i18n key on failure
 * (CLAUDE.md §3); the client maps it to a localized message via t().
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
    return this._post<RoomDto>('/api/rooms', body, init);
  }

  joinRoom(
    code: string,
    body: JoinRoomRequestBody,
    init: RequestInit = {},
  ): Promise<ApiResult<RoomDto>> {
    return this._post<RoomDto>(`/api/rooms/${encodeURIComponent(code)}/join`, body, init);
  }

  async getRoom(code: string, init: RequestInit = {}): Promise<ApiResult<RoomDto>> {
    const res = await this._fetch(`${this._baseUrl}/api/rooms/${encodeURIComponent(code)}`, {
      ...init,
      credentials: 'include',
    });
    return this._readResponse<RoomDto>(res);
  }

  private async _post<T>(
    path: string,
    body: unknown,
    init: RequestInit,
  ): Promise<ApiResult<T>> {
    const res = await this._fetch(`${this._baseUrl}${path}`, {
      ...init,
      method: 'POST',
      credentials: 'include',
      headers: { ...(init.headers ?? {}), 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    });
    return this._readResponse<T>(res);
  }

  private async _readResponse<T>(res: Response): Promise<ApiResult<T>> {
    if (res.ok) {
      const value = (await res.json()) as T;
      return { ok: true, value };
    }
    let problem: ProblemDetailsResponse = {};
    try {
      problem = (await res.json()) as ProblemDetailsResponse;
    } catch {
      // Body wasn't JSON; keep an empty problem.
    }
    return {
      ok: false,
      status: res.status,
      code: problem.code ?? 'errors.unknown',
      ...(problem.detail !== undefined ? { detail: problem.detail } : {}),
    };
  }
}
