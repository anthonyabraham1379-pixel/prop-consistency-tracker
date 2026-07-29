import React from 'react';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { Ionicons } from '@expo/vector-icons';

import DashboardScreen from '../screens/DashboardScreen';
import SettingsScreen from '../screens/SettingsScreen';
import { theme } from '../config/theme';
import { useStrings } from '../config/strings';

const Tab = createBottomTabNavigator();

const TAB_ICONS = {
  Dashboard: 'stats-chart-outline',
  Settings: 'settings-outline',
};

export default function MainTabs() {
  const strings = useStrings();
  return (
    <Tab.Navigator
      screenOptions={({ route }) => ({
        headerShown: false,
        tabBarActiveTintColor: theme.colors.accent,
        tabBarInactiveTintColor: theme.colors.textMuted,
        tabBarStyle: {
          backgroundColor: theme.colors.surface,
          borderTopColor: theme.colors.border,
        },
        tabBarIcon: ({ color, size }) => (
          <Ionicons name={TAB_ICONS[route.name]} color={color} size={size} />
        ),
      })}
    >
      <Tab.Screen name="Dashboard" component={DashboardScreen} options={{ title: strings.tabs.dashboard }} />
      <Tab.Screen name="Settings" component={SettingsScreen} options={{ title: strings.tabs.settings }} />
    </Tab.Navigator>
  );
}
