import React, { createContext, useContext, useEffect, useMemo, useReducer } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';

const STORAGE_KEY = 'propConsistency.challenges';

// Compatibilidad con datos guardados antes de que "days" pasara a llamarse
// "trades" (un challenge podía tener trades individuales o un solo total
// por día bajo el nombre viejo).
function migrateChallenge(challenge) {
  if (challenge.trades) return challenge;
  const { days, ...rest } = challenge;
  return { ...rest, trades: days ?? [] };
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
  };

  return <ChallengesContext.Provider value={value}>{children}</ChallengesContext.Provider>;
}

export function useChallenges() {
  const ctx = useContext(ChallengesContext);
  if (!ctx) throw new Error('useChallenges debe usarse dentro de ChallengesProvider');
  return ctx;
}
