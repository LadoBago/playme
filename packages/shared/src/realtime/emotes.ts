// The fixed catalog of in-match emotes — the web-side mirror of the server
// allowlist in apps/api/src/PlayMe.Domain/Platform/Emote.cs. Keep the two in
// sync. Ids are semantic (not emoji codepoints) so the renderer owns the
// glyph; Phase 2 maps each id to an SVG icon.

export const EMOTE_IDS = ['smile', 'heart', 'clap', 'wow', 'poke', 'cry'] as const;

export type EmoteId = (typeof EMOTE_IDS)[number];
