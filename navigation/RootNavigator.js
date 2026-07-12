import React from 'react';
import { ActivityIndicator, View } from 'react-native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';

import OnboardingScreen from '../screens/OnboardingScreen';
import CalendarScreen from '../screens/CalendarScreen';
import AnalyticsScreen from '../screens/AnalyticsScreen';
import GeneralSettingsScreen from '../screens/GeneralSettingsScreen';
import PaywallScreen from '../screens/PaywallScreen';
import MainTabs from './MainTabs';
import { theme } from '../config/theme';
import { useChallenges } from '../context/ChallengesContext';

const Stack = createNativeStackNavigator();

export default function RootNavigator() {
  const { loading, challenges } = useChallenges();

  if (loading) {
    return (
      <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: theme.colors.background }}>
        <ActivityIndicator color={theme.colors.accent} />
      </View>
    );
  }

  return (
    <Stack.Navigator
      screenOptions={{ headerShown: false }}
      initialRouteName={challenges.length > 0 ? 'Main' : 'Onboarding'}
    >
      <Stack.Screen name="Onboarding" component={OnboardingScreen} />
      <Stack.Screen name="Main" component={MainTabs} />
      <Stack.Screen name="Calendar" component={CalendarScreen} />
      <Stack.Screen name="Analytics" component={AnalyticsScreen} />
      <Stack.Screen name="GeneralSettings" component={GeneralSettingsScreen} />
      <Stack.Screen name="Paywall" component={PaywallScreen} options={{ presentation: 'modal' }} />
    </Stack.Navigator>
  );
}
