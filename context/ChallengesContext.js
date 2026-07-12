import React, { createContext, useContext, useEffect, useMemo, useReducer } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';

const STORAGE_KEY = 'propConsistency.challenges';

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
          c.id === action.payload.id ? { ...c, ...action.payload.updates } : c
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
            ? { ...c, trades: [...c.trades, action.payload.trade] }
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
              }
            : c
        ),
      };
    case 'DELETE_TRADE':
      return {
        ...state,
        challenges: state.challenges.map((c) =>
          c.id === action.payload.challengeId
            ? { ...c, trades: c.trades.filter((t) => t.id !== action.payload.tradeId) }
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
                  { trades: c.trades, archivedAt: new Date().toISOString(), outcome: 'funded' },
                ],
                trades: [],
                breachAcknowledged: false,
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
                  { trades: c.trades, archivedAt: new Date().toISOString(), outcome: 'failed' },
                ],
                trades: [],
                breachAcknowledged: false,
              }
            : c
        ),
      };
    case 'ACK_BREACH':
      return {
        ...state,
        challenges: state.challenges.map((c) =>
          c.id === action.payload.challengeId ? { ...c, breachAcknowledged: true } : c
        ),
      };
    default:
      return state;
  }
}

const ChallengesContext = createContext(null);

export function ChallengesProvider({ children }) {
  const [state, dispatch] = useReducer(reducer, initialState);

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

  const addChallenge = (formValues) => {
    const challenge = {
      ...formValues,
      id: String(Date.now()),
      createdAt: new Date().toISOString(),
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
