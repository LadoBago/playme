'use client';

import { useEffect, useState } from 'react';
import { PlaymeClient, type RoomDto } from '@playme/shared';
import { browserApiBase } from '@/lib/api-base';
import { type ActiveRoom, clearActiveRoom, isResumableStatus, readActiveRoom } from './active-room-storage';

/** The remembered room, and its confirmed snapshot once the GET lands. */
export interface ActiveRoomState {
  stored: ActiveRoom;
  room: RoomDto | null;
}

/**
 * Shared discovery for every "you still have a room" surface. Yields
 * `null` on the server and for a visitor without a remembered room, so
 * SSR markup is never affected. Otherwise it yields the stored key at
 * once (the return link is the point, and in `inProgress` the reconnect
 * grace is only 60–90 s), then confirms with `GET /api/rooms/{code}` and
 * attaches the snapshot. A room that is gone (404) or past the point of
 * return forgets the key and yields `null`; a transport failure keeps the
 * stored key, since the room page is the authority on what's left.
 */
export function useActiveRoom(): ActiveRoomState | null {
  const [state, setState] = useState<ActiveRoomState | null>(null);

  useEffect(() => {
    const active = readActiveRoom();
    if (!active) return undefined;
    setState({ stored: active, room: null });
    let cancelled = false;
    void (async () => {
      const client = new PlaymeClient({ baseUrl: browserApiBase });
      const result = await client.getRoom(active.code, { cache: 'no-store' });
      if (cancelled) return;
      if (result.ok) {
        if (isResumableStatus(result.value.status)) {
          setState({ stored: active, room: result.value });
          return;
        }
        // Post-match or terminal — nothing to come back to.
        clearActiveRoom(active.code);
        setState(null);
        return;
      }
      if (result.status === 404) {
        // Reaped by the sweeper; the key outlived the room.
        clearActiveRoom(active.code);
        setState(null);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  return state;
}
