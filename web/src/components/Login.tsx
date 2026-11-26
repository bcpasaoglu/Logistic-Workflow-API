import { useState } from 'react';
import { api, ApiError, tokenStore } from '../api';
import { Notice } from './Notice';

export function Login({ onSignedIn }: { onSignedIn: () => void }) {
  const [mode, setMode] = useState<'login' | 'register'>('login');
  const [form, setForm] = useState({ fullName: '', lastName: '', email: '', password: '' });
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const set = (key: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm({ ...form, [key]: e.target.value });

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    setInfo(null);
    try {
      if (mode === 'register') {
        await api.register({ ...form, confirmPassword: form.password });
        setInfo('Account created. You can sign in now.');
        setMode('login');
      } else {
        const { accessToken } = await api.login(form.email, form.password);
        tokenStore.set(accessToken);
        onSignedIn();
      }
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Could not reach the API.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="login-page">
      <form className="card login-card" onSubmit={submit}>
        <div className="brand">
          <span className="logo" aria-hidden>
            LD
          </span>
          <span>LogisticDesk</span>
        </div>
        <h1>{mode === 'login' ? 'Sign in' : 'Create an account'}</h1>
        <p className="muted">Dispatch shipments to your fleet with capacity checks.</p>

        {mode === 'register' && (
          <div className="row">
            <label>
              First name
              <input required value={form.fullName} onChange={set('fullName')} />
            </label>
            <label>
              Last name
              <input required value={form.lastName} onChange={set('lastName')} />
            </label>
          </div>
        )}
        <label>
          Email
          <input type="email" required autoComplete="username" value={form.email} onChange={set('email')} />
        </label>
        <label>
          Password
          <input
            type="password"
            required
            autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
            value={form.password}
            onChange={set('password')}
          />
        </label>
        {mode === 'register' && <p className="hint">At least 8 characters with an upper case letter and a digit.</p>}

        {error && <Notice kind="error">{error}</Notice>}
        {info && <Notice kind="success">{info}</Notice>}

        <button className="btn btn-dark btn-block" disabled={busy}>
          {busy ? 'Please wait…' : mode === 'login' ? 'Sign in' : 'Create account'}
        </button>
        <button
          type="button"
          className="btn-link"
          onClick={() => {
            setMode(mode === 'login' ? 'register' : 'login');
            setError(null);
          }}
        >
          {mode === 'login' ? 'No account yet? Register' : 'Already registered? Sign in'}
        </button>
      </form>
    </div>
  );
}
