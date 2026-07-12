import React from 'react';
import { StatusBar } from 'expo-status-bar';
import { NavigationContainer, DarkTheme } from '@react-navigation/native';
import { SafeAreaProvider } from 'react-native-safe-area-context';

import { ChallengesProvider } from './context/ChallengesContext';
import { PreferencesProvider } from './context/PreferencesContext';
import { AuthProvider } from './context/AuthContext';
import { PremiumProvider } from './context/PremiumContext';
import RootNavigator from './navigation/RootNavigator';
import { theme } from './config/theme';

const navigationTheme = {
  ...DarkTheme,
  colors: {
    ...DarkTheme.colors,
    background: theme.colors.background,
    card: theme.colors.surface,
    border: theme.colors.border,
    text: theme.colors.textPrimary,
    primary: theme.colors.accent,
  },
};

export default function App() {
  return (
    <SafeAreaProvider>
      <PreferencesProvider>
        <AuthProvider>
          <PremiumProvider>
            <ChallengesProvider>
              <NavigationContainer theme={navigationTheme}>
                <StatusBar style="light" />
                <RootNavigator />
              </NavigationContainer>
            </ChallengesProvider>
          </PremiumProvider>
        </AuthProvider>
      </PreferencesProvider>
    </SafeAreaProvider>
  );
}
