import React, { createContext, useContext, useEffect, useState } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { DEFAULT_LANGUAGE, SUPPORTED_LANGUAGES } from '../config/language';

const STORAGE_KEY = 'propConsistency.preferences';

const PreferencesContext = createContext(null);

export function PreferencesProvider({ children }) {
  const [hidePnl, setHidePnlState] = useState(false);
  const [hasSeenGuide, setHasSeenGuideState] = useState(false);
  const [language, setLanguageState] = useState(DEFAULT_LANGUAGE);
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    AsyncStorage.getItem(STORAGE_KEY)
      .then((raw) => {
        if (raw) {
          const parsed = JSON.parse(raw);
          setHidePnlState(!!parsed.hidePnl);
          setHasSeenGuideState(!!parsed.hasSeenGuide);
          if (SUPPORTED_LANGUAGES.includes(parsed.language)) {
            setLanguageState(parsed.language);
          }
        }
      })
      .catch(() => {})
      .finally(() => setLoaded(true));
  }, []);

  const persist = (updates) => {
    AsyncStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({ hidePnl, hasSeenGuide, language, ...updates })
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

  const setLanguage = (value) => {
    if (!SUPPORTED_LANGUAGES.includes(value)) return;
    setLanguageState(value);
    persist({ language: value });
  };

  return (
    <PreferencesContext.Provider
      value={{ loaded, hidePnl, setHidePnl, hasSeenGuide, setHasSeenGuide, language, setLanguage }}
    >
      {children}
    </PreferencesContext.Provider>
  );
}

export function usePreferences() {
  const ctx = useContext(PreferencesContext);
  if (!ctx) throw new Error('usePreferences debe usarse dentro de PreferencesProvider');
  return ctx;
}
