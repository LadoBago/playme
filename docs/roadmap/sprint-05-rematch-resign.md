# Sprint 5 — Rematch + resign (~1 week)

- Rematch handshake: `OfferRematch`, `AcceptRematch`, `RejectRematch`. The asymmetric exit from [`platform.md`](../platform.md) §1 #10 (rejector auto-redirects; offerer stays with a notice and a manual exit button).
- Resign with the confirmation step ([`platform.md`](../platform.md) §1 #8).
- `Ended` and `AwaitingRematch` states wired per [`state.md`](../state.md) §2.
- **Series scoreboard** ([`platform.md`](../platform.md) §1 #13): server-side counter in the room state (`{ host, challenger, draws }`), updated on every `MatchEnded`, displayed in the in-match UI for both players. Reset only when the room reaches `Closed`/`Expired`.
- **Side swap on rematch** ([`platform.md`](../platform.md) §1 #15): on every accepted rematch, the server swaps `hostSide` and `challengerSide` before emitting `MatchStarted`. UI shows each player's current side in the HUD so the swap is obvious to both players.

**Exit criteria:** All four games can be played, resigned, finished, rematched (accepted/rejected), and exited cleanly.
