import React, { useState } from 'react';
import { ChevronLeft, Settings, Plus, TrendingUp, TrendingDown, AlertTriangle, CheckCircle2, Check } from 'lucide-react';

const PRESETS = [
  { label: 'Tradeify Select · 100k', accountSize: 100000, profitTarget: 6000, maxDrawdown: 3000, consistencyLimitPct: 40, minProfitableDays: 3 },
  { label: 'FTMO · 100k', accountSize: 100000, profitTarget: 10000, maxDrawdown: 10000, consistencyLimitPct: null, minProfitableDays: 4 },
  { label: 'Personalizado', accountSize: null, profitTarget: null, maxDrawdown: null, consistencyLimitPct: null, minProfitableDays: 1 },
];

const SAMPLE_DAYS = [
  { id: 1, amount: 1450, date: '05/07/2026' },
  { id: 2, amount: -320, date: '06/07/2026' },
  { id: 3, amount: 1180, date: '07/07/2026' },
  { id: 4, amount: 980, date: '08/07/2026' },
];

export default function AppMockup() {
  const [screen, setScreen] = useState('dashboard'); // 'onboarding' | 'dashboard' | 'settings'
  const [challenge, setChallenge] = useState({
    name: 'Tradeify Select 100k',
    accountSize: 100000,
    profitTarget: 6000,
    maxDrawdown: 3000,
    drawdownType: 'static',
    consistencyLimitPct: 40,
    minProfitableDays: 3,
    days: SAMPLE_DAYS,
  });

  return (
    <div style={styles.phoneWrap}>
      <div style={styles.phone}>
        {screen === 'onboarding' && (
          <OnboardingScreen challenge={challenge} setChallenge={setChallenge} onDone={() => setScreen('dashboard')} />
        )}
        {screen === 'dashboard' && (
          <DashboardScreen challenge={challenge} onSettings={() => setScreen('settings')} onNewChallenge={() => setScreen('onboarding')} />
        )}
        {screen === 'settings' && (
          <SettingsScreen challenge={challenge} setChallenge={setChallenge} onBack={() => setScreen('dashboard')} />
        )}
      </div>
      <div style={styles.tabRow}>
        {['onboarding', 'dashboard', 'settings'].map((s) => (
          <button key={s} onClick={() => setScreen(s)} style={styles.tabBtn(screen === s)}>
            {s === 'onboarding' ? 'Nuevo challenge' : s === 'dashboard' ? 'Dashboard' : 'Configuración'}
          </button>
        ))}
      </div>
    </div>
  );
}

/* ---------------- ONBOARDING ---------------- */
function OnboardingScreen({ challenge, setChallenge, onDone }) {
  const [form, setForm] = useState(challenge);
  const [presetIdx, setPresetIdx] = useState(0);

  const applyPreset = (idx) => {
    setPresetIdx(idx);
    const p = PRESETS[idx];
    setForm((f) => ({
      ...f,
      name: p.label === 'Personalizado' ? '' : p.label,
      accountSize: p.accountSize ?? f.accountSize,
      profitTarget: p.profitTarget ?? f.profitTarget,
      maxDrawdown: p.maxDrawdown ?? f.maxDrawdown,
      consistencyLimitPct: p.consistencyLimitPct,
      minProfitableDays: p.minProfitableDays,
    }));
  };

  return (
    <Screen>
      <ScreenHeader eyebrow="NUEVO CHALLENGE" title="Configura tu cuenta" sub="Define los parámetros de tu evaluación o cuenta fondeada" />

      <FieldLabel>Elige un punto de partida</FieldLabel>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8, marginBottom: 18 }}>
        {PRESETS.map((p, i) => (
          <button key={p.label} onClick={() => applyPreset(i)} style={styles.presetCard(presetIdx === i)}>
            <span>{p.label}</span>
            {presetIdx === i && <Check size={16} color="#5eb3f6" strokeWidth={2.5} />}
          </button>
        ))}
      </div>

      <FieldLabel>Nombre de la cuenta</FieldLabel>
      <input style={styles.input} value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} placeholder="Ej. Tradeify Select 100k" />

      <Row>
        <NumField label="Tamaño de cuenta" value={form.accountSize} onChange={(v) => setForm({ ...form, accountSize: v })} prefix="$" />
        <NumField label="Objetivo de profit" value={form.profitTarget} onChange={(v) => setForm({ ...form, profitTarget: v })} prefix="$" />
      </Row>
      <Row>
        <NumField label="Drawdown máximo" value={form.maxDrawdown} onChange={(v) => setForm({ ...form, maxDrawdown: v })} prefix="$" />
        <NumField label="Días mín. rentables" value={form.minProfitableDays} onChange={(v) => setForm({ ...form, minProfitableDays: v })} />
      </Row>

      <FieldLabel>Tipo de drawdown</FieldLabel>
      <SegmentedControl
        options={[{ label: 'Estático', value: 'static' }, { label: 'Trailing', value: 'trailing' }]}
        value={form.drawdownType}
        onChange={(v) => setForm({ ...form, drawdownType: v })}
      />

      <FieldLabel style={{ marginTop: 14 }}>Regla de consistencia</FieldLabel>
      <SegmentedControl
        options={[{ label: 'Sin regla', value: null }, { label: '30%', value: 30 }, { label: '40%', value: 40 }]}
        value={form.consistencyLimitPct}
        onChange={(v) => setForm({ ...form, consistencyLimitPct: v })}
      />

      <button
        style={{ ...styles.primaryBtn, marginTop: 24 }}
        onClick={() => { setChallenge({ ...form, days: challenge.days }); onDone(); }}
      >
        Crear cuenta
      </button>
      <p style={styles.hint}>Podrás editar todos estos valores después desde Configuración.</p>
    </Screen>
  );
}

/* ---------------- DASHBOARD ---------------- */
function DashboardScreen({ challenge, onSettings, onNewChallenge }) {
  const { days } = challenge;
  const totalProfit = days.reduce((s, d) => s + d.amount, 0);
  const profitable = days.filter((d) => d.amount > 0);
  const totalPositive = profitable.reduce((s, d) => s + d.amount, 0);
  const bestDay = profitable.length ? Math.max(...profitable.map((d) => d.amount)) : 0;
  const consistencyPct = totalPositive > 0 ? (bestDay / totalPositive) * 100 : 0;
  const limit = challenge.consistencyLimitPct;
  const isCompliant = !limit || consistencyPct <= limit;
  const targetProgress = Math.min((totalProfit / challenge.profitTarget) * 100, 100);

  let running = 0, peak = 0, maxDD = 0;
  for (const d of days) { running += d.amount; if (running > peak) peak = running; const dd = peak - running; if (dd > maxDD) maxDD = dd; }

  return (
    <Screen>
      <div style={styles.dashTopRow}>
        <div>
          <div style={styles.headerEyebrow}>{challenge.name.toUpperCase()}</div>
          <h1 style={styles.headerTitle}>Dashboard</h1>
        </div>
        <button onClick={onSettings} style={styles.iconBtn}><Settings size={20} color="#8b93a1" /></button>
      </div>

      <div style={styles.statusCard(isCompliant)}>
        <div>{isCompliant ? <CheckCircle2 size={26} color="#4ade80" /> : <AlertTriangle size={26} color="#f87171" />}</div>
        <div>
          <div style={styles.statusPct}>{limit ? `${consistencyPct.toFixed(1)}%` : '—'}</div>
          <div style={styles.statusLabel}>
            {!limit ? 'Sin regla de consistencia configurada' : isCompliant ? `Cumples el límite de ${limit}%` : `Excede el límite de ${limit}%`}
          </div>
        </div>
      </div>

      <div style={styles.grid}>
        <MetricCard label="Ganancia total" value={`$${totalProfit.toLocaleString('en-US')}`} accent="#4ade80" />
        <MetricCard label="Progreso a meta" value={`${targetProgress.toFixed(0)}%`} accent="#5eb3f6" bar={targetProgress} />
        <MetricCard label="Mejor día" value={`$${bestDay.toLocaleString('en-US')}`} accent="#f0b429" />
        <MetricCard label="Drawdown usado" value={`$${maxDD} / $${challenge.maxDrawdown}`} accent={maxDD > challenge.maxDrawdown * 0.75 ? '#f87171' : '#8b93a1'} />
      </div>

      <div style={styles.infoBar}>
        <span>Días: <strong>{days.length}</strong></span>
        <span style={{ color: '#3a4150' }}>·</span>
        <span>Rentables: <strong>{profitable.length}</strong> / {challenge.minProfitableDays} mín.</span>
      </div>

      <FieldLabel style={{ marginTop: 16 }}>Últimos días</FieldLabel>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        {[...days].reverse().map((d) => (
          <div key={d.id} style={styles.dayRow}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              {d.amount >= 0 ? <TrendingUp size={15} color="#4ade80" /> : <TrendingDown size={15} color="#f87171" />}
              <span style={{ fontSize: 13, color: '#b4bac4' }}>{d.date}</span>
            </div>
            <span style={{ fontWeight: 700, color: d.amount >= 0 ? '#4ade80' : '#f87171' }}>
              {d.amount >= 0 ? '+' : ''}${d.amount.toLocaleString('en-US')}
            </span>
          </div>
        ))}
      </div>

      <button style={{ ...styles.primaryBtn, marginTop: 18 }}><Plus size={16} style={{ marginRight: 6 }} />Registrar día</button>
      <button onClick={onNewChallenge} style={styles.ghostBtn}>+ Agregar otra cuenta/challenge</button>
    </Screen>
  );
}

/* ---------------- SETTINGS ---------------- */
function SettingsScreen({ challenge, setChallenge, onBack }) {
  const [form, setForm] = useState(challenge);
  const [saved, setSaved] = useState(false);

  const save = () => {
    setChallenge(form);
    setSaved(true);
    setTimeout(() => setSaved(false), 1500);
  };

  return (
    <Screen>
      <div style={styles.dashTopRow}>
        <button onClick={onBack} style={styles.iconBtn}><ChevronLeft size={22} color="#8b93a1" /></button>
        <div style={{ flex: 1 }}>
          <div style={styles.headerEyebrow}>EDITAR PARÁMETROS</div>
          <h1 style={styles.headerTitle}>Configuración</h1>
        </div>
      </div>

      <FieldLabel>Nombre de la cuenta</FieldLabel>
      <input style={styles.input} value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />

      <Row>
        <NumField label="Tamaño de cuenta" value={form.accountSize} onChange={(v) => setForm({ ...form, accountSize: v })} prefix="$" />
        <NumField label="Objetivo de profit" value={form.profitTarget} onChange={(v) => setForm({ ...form, profitTarget: v })} prefix="$" />
      </Row>
      <Row>
        <NumField label="Drawdown máximo" value={form.maxDrawdown} onChange={(v) => setForm({ ...form, maxDrawdown: v })} prefix="$" />
        <NumField label="Días mín. rentables" value={form.minProfitableDays} onChange={(v) => setForm({ ...form, minProfitableDays: v })} />
      </Row>

      <FieldLabel>Tipo de drawdown</FieldLabel>
      <SegmentedControl
        options={[{ label: 'Estático', value: 'static' }, { label: 'Trailing', value: 'trailing' }]}
        value={form.drawdownType}
        onChange={(v) => setForm({ ...form, drawdownType: v })}
      />

      <FieldLabel style={{ marginTop: 14 }}>Regla de consistencia</FieldLabel>
      <SegmentedControl
        options={[{ label: 'Sin regla', value: null }, { label: '30%', value: 30 }, { label: '40%', value: 40 }]}
        value={form.consistencyLimitPct}
        onChange={(v) => setForm({ ...form, consistencyLimitPct: v })}
      />

      <button style={{ ...styles.primaryBtn, marginTop: 24 }} onClick={save}>
        {saved ? <><Check size={16} style={{ marginRight: 6 }} />Guardado</> : 'Guardar cambios'}
      </button>

      <button style={styles.dangerGhostBtn}>Eliminar esta cuenta/challenge</button>
    </Screen>
  );
}

/* ---------------- Shared bits ---------------- */
function Screen({ children }) { return <div style={styles.screen}>{children}</div>; }
function ScreenHeader({ eyebrow, title, sub }) {
  return (
    <div style={{ marginBottom: 20 }}>
      <div style={styles.headerEyebrow}>{eyebrow}</div>
      <h1 style={styles.headerTitle}>{title}</h1>
      {sub && <p style={styles.headerSub}>{sub}</p>}
    </div>
  );
}
function FieldLabel({ children, style }) { return <div style={{ ...styles.fieldLabel, ...style }}>{children}</div>; }
function Row({ children }) { return <div style={{ display: 'flex', gap: 10 }}>{children}</div>; }
function NumField({ label, value, onChange, prefix }) {
  return (
    <div style={{ flex: 1, marginBottom: 12 }}>
      <FieldLabel>{label}</FieldLabel>
      <div style={styles.numFieldWrap}>
        {prefix && <span style={styles.numPrefix}>{prefix}</span>}
        <input
          type="text" inputMode="decimal"
          style={styles.numInput}
          value={value ?? ''}
          onChange={(e) => onChange(e.target.value.replace(/[^0-9.]/g, ''))}
        />
      </div>
    </div>
  );
}
function SegmentedControl({ options, value, onChange }) {
  return (
    <div style={{ display: 'flex', gap: 8, marginBottom: 12 }}>
      {options.map((o) => (
        <button key={String(o.value)} onClick={() => onChange(o.value)} style={styles.segBtn(value === o.value)}>
          {o.label}
        </button>
      ))}
    </div>
  );
}
function MetricCard({ label, value, accent, bar }) {
  return (
    <div style={styles.metricCard}>
      <div style={styles.metricLabel}>{label}</div>
      <div style={{ fontSize: 18, fontWeight: 700, color: accent }}>{value}</div>
      {typeof bar === 'number' && (
        <div style={styles.barTrack}><div style={{ ...styles.barFill, width: `${bar}%`, background: accent }} /></div>
      )}
    </div>
  );
}

/* ---------------- Styles ---------------- */
const styles = {
  phoneWrap: { display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12, background: '#05070a', padding: 24, minHeight: '100vh', fontFamily: "'Inter', system-ui, sans-serif" },
  phone: { width: 380, maxWidth: '100%', height: 720, background: '#0b0e14', borderRadius: 28, border: '1px solid #1f2530', overflowY: 'auto', boxShadow: '0 30px 60px rgba(0,0,0,0.5)' },
  screen: { padding: '22px 18px 30px' },
  tabRow: { display: 'flex', gap: 8 },
  tabBtn: (active) => ({ padding: '8px 14px', borderRadius: 20, border: `1px solid ${active ? '#5eb3f6' : '#1f2530'}`, background: active ? '#5eb3f61f' : 'transparent', color: active ? '#5eb3f6' : '#5a6272', fontSize: 12, fontWeight: 600, cursor: 'pointer' }),
  headerEyebrow: { fontSize: 11, letterSpacing: '0.08em', color: '#5eb3f6', fontWeight: 700, marginBottom: 6 },
  headerTitle: { fontSize: 22, fontWeight: 700, color: '#e4e7ec', margin: 0, letterSpacing: '-0.02em' },
  headerSub: { fontSize: 13, color: '#8b93a1', marginTop: 4 },
  dashTopRow: { display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 18, gap: 8 },
  iconBtn: { background: '#141824', border: '1px solid #1f2530', borderRadius: 10, width: 38, height: 38, display: 'flex', alignItems: 'center', justifyContent: 'center', cursor: 'pointer' },
  fieldLabel: { fontSize: 12, color: '#8b93a1', marginBottom: 6, fontWeight: 600 },
  input: { width: '100%', background: '#141824', border: '1px solid #1f2530', borderRadius: 10, padding: '11px 13px', color: '#e4e7ec', fontSize: 14, outline: 'none', marginBottom: 14, boxSizing: 'border-box' },
  numFieldWrap: { display: 'flex', alignItems: 'center', background: '#141824', border: '1px solid #1f2530', borderRadius: 10, padding: '0 12px' },
  numPrefix: { color: '#5a6272', fontSize: 14, marginRight: 4 },
  numInput: { flex: 1, background: 'transparent', border: 'none', padding: '11px 0', color: '#e4e7ec', fontSize: 14, outline: 'none', width: '100%' },
  presetCard: (active) => ({ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '12px 14px', borderRadius: 10, border: `1px solid ${active ? '#5eb3f6' : '#1f2530'}`, background: active ? '#5eb3f614' : '#141824', color: '#e4e7ec', fontSize: 13.5, cursor: 'pointer', textAlign: 'left' }),
  segBtn: (active) => ({ flex: 1, padding: '10px 0', borderRadius: 10, border: `1px solid ${active ? '#5eb3f6' : '#1f2530'}`, background: active ? '#5eb3f61f' : '#141824', color: active ? '#5eb3f6' : '#8b93a1', fontSize: 13, fontWeight: 600, cursor: 'pointer' }),
  primaryBtn: { width: '100%', background: '#5eb3f6', color: '#0b0e14', border: 'none', borderRadius: 12, padding: '14px 0', fontSize: 14.5, fontWeight: 700, cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center' },
  ghostBtn: { width: '100%', background: 'transparent', color: '#5eb3f6', border: 'none', padding: '12px 0', fontSize: 13.5, fontWeight: 600, cursor: 'pointer' },
  dangerGhostBtn: { width: '100%', background: 'transparent', color: '#f87171', border: 'none', padding: '14px 0', fontSize: 13, fontWeight: 600, cursor: 'pointer', marginTop: 6 },
  hint: { fontSize: 11.5, color: '#5a6272', textAlign: 'center', marginTop: 10, lineHeight: 1.4 },
  statusCard: (ok) => ({ display: 'flex', alignItems: 'center', gap: 14, padding: '16px 18px', borderRadius: 14, background: ok ? 'rgba(74,222,128,0.08)' : 'rgba(248,113,113,0.08)', border: `1px solid ${ok ? 'rgba(74,222,128,0.25)' : 'rgba(248,113,113,0.25)'}`, marginBottom: 16 }),
  statusPct: { fontSize: 24, fontWeight: 800, color: '#e4e7ec', lineHeight: 1 },
  statusLabel: { fontSize: 12.5, color: '#b4bac4', marginTop: 4 },
  grid: { display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10, marginBottom: 16 },
  metricCard: { background: '#141824', border: '1px solid #1f2530', borderRadius: 12, padding: '13px 14px' },
  metricLabel: { fontSize: 11, color: '#8b93a1', marginBottom: 6 },
  barTrack: { height: 4, background: '#1f2530', borderRadius: 2, marginTop: 8, overflow: 'hidden' },
  barFill: { height: '100%', borderRadius: 2 },
  infoBar: { display: 'flex', gap: 8, fontSize: 12.5, color: '#8b93a1', padding: '10px 14px', background: '#141824', borderRadius: 10, border: '1px solid #1f2530' },
  dayRow: { display: 'flex', justifyContent: 'space-between', alignItems: 'center', background: '#141824', border: '1px solid #1f2530', borderRadius: 10, padding: '10px 14px' },
};
