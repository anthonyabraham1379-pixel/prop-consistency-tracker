import React, { createContext, useContext, useEffect, useState } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';

const STORAGE_KEY = 'propConsistency.preferences';

const PreferencesContext = createContext(null);

export function PreferencesProvider({ children }) {
  const [hidePnl, setHidePnlState] = useState(false);
  const [hasSeenGuide, setHasSeenGuideState] = useState(false);
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    AsyncStorage.getItem(STORAGE_KEY)
      .then((raw) => {
        if (raw) {
          const parsed = JSON.parse(raw);
          setHidePnlState(!!parsed.hidePnl);
          setHasSeenGuideState(!!parsed.hasSeenGuide);
        }
      })
      .catch(() => {})
      .finally(() => setLoaded(true));
  }, []);

  const persist = (updates) => {
    AsyncStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({ hidePnl, hasSeenGuide, ...updates })
    ).catch(() => {});
  };

  const setHidePnl = (value) => {
    setHidePnlState(value);
    persist({ hidePnl: value });
  };

  const setHasSeenGuide = (value) => {
    setHasSeenGuideState(value);
    persist({ hasSeenGuide: value });
  };

  return (
    <PreferencesContext.Provider value={{ loaded, hidePnl, setHidePnl, hasSeenGuide, setHasSeenGuide }}>
      {children}
    </PreferencesContext.Provider>
  );
}

export function usePreferences() {
  const ctx = useContext(PreferencesContext);
  if (!ctx) throw new Error('usePreferences debe usarse dentro de PreferencesProvider');
  return ctx;
}
