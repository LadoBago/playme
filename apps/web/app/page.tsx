import { headers } from 'next/headers';

export const dynamic = 'force-dynamic';

type HealthResponse = {
  status: string;
  service: string;
  version: string;
  timestamp: string;
};

type HealthCheck =
  | { ok: true; payload: HealthResponse; url: string }
  | { ok: false; error: string; url: string };

async function checkApiHealth(): Promise<HealthCheck> {
  const apiUrl = process.env.PLAYME_API_URL ?? 'http://localhost:5080';
  const target = `${apiUrl.replace(/\/$/, '')}/api/health`;
  try {
    const res = await fetch(target, { cache: 'no-store' });
    if (!res.ok) {
      return { ok: false, error: `HTTP ${res.status}`, url: target };
    }
    const payload = (await res.json()) as HealthResponse;
    return { ok: true, payload, url: target };
  } catch (err) {
    const message = err instanceof Error ? err.message : 'unknown error';
    return { ok: false, error: message, url: target };
  }
}

export default async function HomePage() {
  // headers() is awaited only to opt this route into dynamic rendering;
  // the actual value is unused (Sprint 0 placeholder).
  await headers();
  const health = await checkApiHealth();

  return (
    <main
      style={{
        minHeight: '100vh',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        padding: '2rem',
        gap: '1.5rem',
        textAlign: 'center',
      }}
    >
      <h1 style={{ margin: 0, fontSize: '2.5rem' }}>PlayMe</h1>
      <p style={{ margin: 0, opacity: 0.7 }}>
        Real-time, anonymous, two-player casual games. Sprint 0 placeholder.
      </p>

      <section
        style={{
          marginTop: '1rem',
          padding: '1rem 1.5rem',
          border: '1px solid rgba(127, 127, 127, 0.4)',
          borderRadius: 12,
          minWidth: 320,
        }}
      >
        <h2 style={{ margin: '0 0 0.5rem', fontSize: '1rem' }}>API health</h2>
        {health.ok ? (
          <pre
            style={{
              margin: 0,
              textAlign: 'left',
              fontFamily: 'ui-monospace, SFMono-Regular, Menlo, monospace',
              fontSize: '0.85rem',
            }}
          >
            {JSON.stringify(health.payload, null, 2)}
          </pre>
        ) : (
          <p style={{ margin: 0, color: '#b00020' }}>
            Could not reach API at <code>{health.url}</code>: {health.error}
          </p>
        )}
      </section>
    </main>
  );
}
