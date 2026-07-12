import React, { createContext, useContext, useEffect, useMemo, useReducer, useRef } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { supabase } from '../config/supabase';
import { useAuth } from './AuthContext';

const STORAGE_KEY = 'propConsistency.challenges';

function nowIso() {
  return new Date().toISOString();
}

// Compatibilidad con datos guardados antes de que "days" pasara a llamarse
// "trades" (un challenge podía tener trades individuales o un solo total
// por día bajo el nombre viejo), y antes de que "evaluationArchive" (un solo
// archivo) pasara a ser "archivedPhases" (una lista, para poder reiniciar
// más de una vez).
function migrateChallenge(challenge) {
  let migrated = challenge;
  if (!migrated.trades) {
    const { days, ...rest } = migrated;
    migrated = { ...rest, trades: days ?? [] };
  }
  if (!migrated.status) {
    migrated = { ...migrated, status: 'evaluation' };
  }
  if (!migrated.archivedPhases) {
    const legacyArchive = migrated.evaluationArchive;
    const { evaluationArchive, ...rest } = migrated;
    migrated = {
      ...rest,
      archivedPhases: legacyArchive ? [{ ...legacyArchive, outcome: 'funded' }] : [],
    };
  }
  if (migrated.breachAcknowledged === undefined) {
    migrated = { ...migrated, breachAcknowledged: false };
  }
  if (!migrated.updatedAt) {
    migrated = { ...migrated, updatedAt: migrated.createdAt ?? nowIso() };
  }
  return migrated;
}

const initialState = {
  loading: true,
  challenges: [],
  activeChallengeId: null,
};

function reducer(state, action) {
  switch (action.type) {
    case 'LOADED':
      return {
        ...state,
        loading: false,
        challenges: action.payload.challenges,
        activeChallengeId: action.payload.activeChallengeId,
      };
    case 'ADD_CHALLENGE':
      return {
        ...state,
        challenges: [...state.challenges, action.payload],
        activeChallengeId: action.payload.id,
      };
    case 'SET_ACTIVE':
      return { ...state, activeChallengeId: action.payload };
    case 'UPDATE_CHALLENGE':
      return {
        ...state,
        challenges: state.challenges.map((c) =>
          c.id === action.payload.id ? { ...c, ...action.payload.updates, updatedAt: nowIso() } : c
        ),
      };
    case 'DELETE_CHALLENGE': {
      const remaining = state.challenges.filter((c) => c.id !== action.payload);
      const wasActive = state.activeChallengeId === action.payload;
      return {
        ...state,
        challenges: remaining,
        activeChallengeId: wasActive ? (remaining[0]?.id ?? null) : state.activeChallengeId,
      };
    }
    case 'ADD_TRADE':
      return {
        ...state,
        challenges: state.challenges.map((c) =>
          c.id === action.payload.challengeId
            ? { ...c, trades: [...c.trades, action.payload.trade], updatedAt: nowIso() }
            : c
        ),
      };
    case 'UPDATE_TRADE':
      return {
        ...state,
        challenges: state.challenges.map((c) =>
          c.id === action.payload.challengeId
            ? {
                ...c,
                trades: c.trades.map((t) =>
                  t.id === action.payload.tradeId ? { ...t, ...action.payload.updates } : t
                ),
                updatedAt: nowIso(),
              }
            : c
        ),
      };
    case 'DELETE_TRADE':
      return {
        ...state,
        challenges: state.challenges.map((c) =>
          c.id === action.payload.challengeId
            ? { ...c, trades: c.trades.filter((t) => t.id !== action.payload.tradeId), updatedAt: nowIso() }
            : c
        ),
      };
    case 'UPGRADE_TO_FUNDED':
      return {
        ...state,
        challenges: state.challenges.map((c) =>
          c.id === action.payload.challengeId
            ? {
                ...c,
                status: 'funded',
                archivedPhases: [
                  ...c.archivedPhases,
                  { trades: c.trades, archivedAt: nowIso(), outcome: 'funded' },
                ],
                trades: [],
                breachAcknowledged: false,
                updatedAt: nowIso(),
              }
            : c
        ),
      };
    case 'RESTART_EVALUATION':
      return {
        ...state,
        challenges: state.challenges.map((c) =>
          c.id === action.payload.challengeId
            ? {
                ...c,
                archivedPhases: [
                  ...c.archivedPhases,
                  { trades: c.trades, archivedAt: nowIso(), outcome: 'failed' },
                ],
                trades: [],
                breachAcknowledged: false,
                updatedAt: nowIso(),
              }
            : c
        ),
      };
    case 'ACK_BREACH':
      return {
        ...state,
        challenges: state.challenges.map((c) =>
          c.id === action.payload.challengeId ? { ...c, breachAcknowledged: true, updatedAt: nowIso() } : c
        ),
      };
    case 'MERGE_REMOTE': {
      const merged = [...state.challenges];
      for (const remote of action.payload.remoteChallenges) {
        const idx = merged.findIndex((c) => c.id === remote.id);
        if (idx === -1) {
          merged.push(remote);
        } else {
          const localTime = new Date(merged[idx].updatedAt ?? 0).getTime();
          const remoteTime = new Date(remote.updatedAt ?? 0).getTime();
          if (remoteTime > localTime) merged[idx] = remote;
        }
      }
      return {
        ...state,
        challenges: merged,
        activeChallengeId: state.activeChallengeId ?? merged[0]?.id ?? null,
      };
    }
    default:
      return state;
  }
}

const ChallengesContext = createContext(null);

export function ChallengesProvider({ children }) {
  const { user } = useAuth();
  const [state, dispatch] = useReducer(reducer, initialState);
  const hasPulledForUserRef = useRef(null);

  useEffect(() => {
    AsyncStorage.getItem(STORAGE_KEY)
      .then((raw) => {
        const parsed = raw ? JSON.parse(raw) : { challenges: [], activeChallengeId: null };
        dispatch({
          type: 'LOADED',
          payload: { ...parsed, challenges: parsed.challenges.map(migrateChallenge) },
        });
      })
      .catch(() => dispatch({ type: 'LOADED', payload: { challenges: [], activeChallengeId: null } }));
  }, []);

  useEffect(() => {
    if (state.loading) return;
    const { challenges, activeChallengeId } = state;
    AsyncStorage.setItem(STORAGE_KEY, JSON.stringify({ challenges, activeChallengeId })).catch(() => {});
  }, [state.loading, state.challenges, state.activeChallengeId]);

  // Al iniciar sesión, trae los challenges guardados en la nube y los
  // fusiona con los locales (el más reciente por updatedAt gana).
  useEffect(() => {
    if (!user) {
      hasPulledForUserRef.current = null;
      return;
    }
    if (state.loading) return;
    if (hasPulledForUserRef.current === user.id) return;
    hasPulledForUserRef.current = user.id;

    supabase
      .from('challenges')
      .select('data')
      .eq('user_id', user.id)
      .then(({ data, error }) => {
        if (error || !data) return;
        const remoteChallenges = data.map((row) => row.data);
        if (remoteChallenges.length > 0) {
          dispatch({ type: 'MERGE_REMOTE', payload: { remoteChallenges } });
        }
      });
  }, [user, state.loading]);

  // Mientras haya sesión, sube cualquier cambio local a Supabase (con
  // un pequeño debounce para no disparar una escritura por cada tecla).
  useEffect(() => {
    if (!user || state.loading) return;
    const timeout = setTimeout(() => {
      state.challenges.forEach((c) => {
        supabase
          .from('challenges')
          .upsert({ id: c.id, user_id: user.id, data: c, updated_at: c.updatedAt ?? nowIso() })
          .then(({ error }) => {
            if (error) console.warn('Error al sincronizar challenge:', error.message);
          });
      });
    }, 1500);
    return () => clearTimeout(timeout);
  }, [user, state.challenges, state.loading]);

  const addChallenge = (formValues) => {
    const challenge = {
      ...formValues,
      id: String(Date.now()),
      createdAt: nowIso(),
      updatedAt: nowIso(),
      trades: [],
      archivedPhases: [],
      breachAcknowledged: false,
    };
    dispatch({ type: 'ADD_CHALLENGE', payload: challenge });
    return challenge;
  };

  const setActiveChallenge = (id) => dispatch({ type: 'SET_ACTIVE', payload: id });

  const updateChallenge = (id, updates) => dispatch({ type: 'UPDATE_CHALLENGE', payload: { id, updates } });

  const deleteChallenge = (id) => dispatch({ type: 'DELETE_CHALLENGE', payload: id });

  const addTrade = (challengeId, { amount, date }) => {
    const trade = { id: `${Date.now()}-${Math.round(Math.random() * 1000)}`, amount, date };
    dispatch({ type: 'ADD_TRADE', payload: { challengeId, trade } });
  };

  const updateTrade = (challengeId, tradeId, updates) =>
    dispatch({ type: 'UPDATE_TRADE', payload: { challengeId, tradeId, updates } });

  const deleteTrade = (challengeId, tradeId) =>
    dispatch({ type: 'DELETE_TRADE', payload: { challengeId, tradeId } });

  const upgradeToFunded = (challengeId) => dispatch({ type: 'UPGRADE_TO_FUNDED', payload: { challengeId } });

  const restartEvaluation = (challengeId) => dispatch({ type: 'RESTART_EVALUATION', payload: { challengeId } });

  const acknowledgeBreach = (challengeId) => dispatch({ type: 'ACK_BREACH', payload: { challengeId } });

  const activeChallenge = useMemo(
    () => state.challenges.find((c) => c.id === state.activeChallengeId) ?? null,
    [state.challenges, state.activeChallengeId]
  );

  const value = {
    loading: state.loading,
    challenges: state.challenges,
    activeChallenge,
    addChallenge,
    setActiveChallenge,
    updateChallenge,
    deleteChallenge,
    addTrade,
    updateTrade,
    deleteTrade,
    upgradeToFunded,
    restartEvaluation,
    acknowledgeBreach,
  };

  return <ChallengesContext.Provider value={value}>{children}</ChallengesContext.Provider>;
}

export function useChallenges() {
  const ctx = useContext(ChallengesContext);
  if (!ctx) throw new Error('useChallenges debe usarse dentro de ChallengesProvider');
  return ctx;
}
