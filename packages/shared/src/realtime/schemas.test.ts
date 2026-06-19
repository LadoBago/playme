import { describe, expect, it } from 'vitest';
import { EmoteReceivedPayloadSchema } from './schemas';

// The emote-received payload is untrusted inbound (a server-pushed SignalR
// message). RoomHubClient parses it through this schema and drops anything
// malformed before calling the handler, so the allowlist is the web-side
// guard mirroring Domain/Platform/Emote.cs.
describe('EmoteReceivedPayloadSchema', () => {
  it('accepts a known emote id from a valid role', () => {
    const parsed = EmoteReceivedPayloadSchema.safeParse({
      from: 'challenger',
      emoteId: 'heart',
    });
    expect(parsed.success).toBe(true);
  });

  it('rejects an emote id outside the allowlist', () => {
    const parsed = EmoteReceivedPayloadSchema.safeParse({
      from: 'host',
      emoteId: 'rocket',
    });
    expect(parsed.success).toBe(false);
  });

  it('rejects an unknown role', () => {
    const parsed = EmoteReceivedPayloadSchema.safeParse({
      from: 'spectator',
      emoteId: 'smile',
    });
    expect(parsed.success).toBe(false);
  });
});
