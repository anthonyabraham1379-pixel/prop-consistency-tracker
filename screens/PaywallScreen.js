import React, { useState } from 'react';
import { ActivityIndicator, Alert, ScrollView, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { useNavigation } from '@react-navigation/native';
import { Ionicons } from '@expo/vector-icons';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { theme } from '../config/theme';
import { strings } from '../config/strings';
import { usePremium } from '../context/PremiumContext';

const FEATURES = [
  { label: strings.premium.featureCalendar, free: true, premium: true },
  { label: strings.premium.featureDrawdown, free: true, premium: true },
  { label: strings.premium.featureNoAds, free: false, premium: true },
  { label: strings.premium.featureAdvancedAnalytics, free: false, premium: true },
  { label: strings.premium.featureMultiAccount, free: false, premium: true },
  { label: strings.premium.featureExport, free: false, premium: true },
  { label: strings.premium.featureScreenshots, free: false, premium: true },
];

function FeatureCheck({ value }) {
  return (
    <Ionicons
      name={value ? 'checkmark-circle' : 'close-circle'}
      size={18}
      color={value ? theme.colors.positive : theme.colors.negative}
    />
  );
}

export default function PaywallScreen() {
  const navigation = useNavigation();
  const insets = useSafeAreaInsets();
  const { offerings, purchasePackage, restorePurchases, isPremium } = usePremium();
  const [purchasing, setPurchasing] = useState(false);
  const [restoring, setRestoring] = useState(false);

  const monthlyPackage =
    offerings?.current?.monthly ?? offerings?.current?.availablePackages?.[0] ?? null;
  const priceString = monthlyPackage?.product?.priceString ?? strings.premium.priceFallback;

  const handleSubscribe = async () => {
    if (!monthlyPackage) {
      Alert.alert(strings.premium.title, strings.premium.purchaseError);
      return;
    }
    setPurchasing(true);
    try {
      await purchasePackage(monthlyPackage);
      navigation.goBack();
    } catch (e) {
      if (!e?.userCancelled) {
        Alert.alert(strings.premium.title, e.message || strings.premium.purchaseError);
      }
    } finally {
      setPurchasing(false);
    }
  };

  const handleRestore = async () => {
    setRestoring(true);
    try {
      await restorePurchases();
      Alert.alert(strings.premium.title, strings.generalSettings.restoreSuccess);
    } catch (e) {
      Alert.alert(strings.premium.title, e.message || strings.generalSettings.restoreError);
    } finally {
      setRestoring(false);
    }
  };

  return (
    <View style={[styles.screen, { paddingTop: insets.top + theme.spacing(2) }]}>
      <View style={styles.header}>
        <TouchableOpacity onPress={() => navigation.goBack()} hitSlop={12}>
          <Ionicons name="close" size={24} color={theme.colors.textSecondary} />
        </TouchableOpacity>
      </View>

      <ScrollView contentContainerStyle={styles.content}>
        <Text style={styles.title}>{strings.premium.title}</Text>
        <Text style={styles.subtitle}>{strings.premium.subtitle}</Text>

        <View style={styles.table}>
          <View style={styles.tableHeaderRow}>
            <Text style={[styles.tableHeaderCell, styles.featureCol]}>{strings.premium.featuresHeader}</Text>
            <Text style={styles.tableHeaderCell}>{strings.premium.freeHeader}</Text>
            <Text style={styles.tableHeaderCell}>{strings.premium.premiumHeader}</Text>
          </View>
          {FEATURES.map((f) => (
            <View key={f.label} style={styles.tableRow}>
              <Text style={[styles.featureLabel, styles.featureCol]}>{f.label}</Text>
              <View style={styles.cell}>
                <FeatureCheck value={f.free} />
              </View>
              <View style={styles.cell}>
                <FeatureCheck value={f.premium} />
              </View>
            </View>
          ))}
        </View>

        {isPremium ? (
          <View style={styles.activeBadge}>
            <Ionicons name="checkmark-circle" size={16} color={theme.colors.positive} />
            <Text style={styles.activeBadgeText}>{strings.generalSettings.premiumActive}</Text>
          </View>
        ) : (
          <View style={styles.priceCard}>
            <View>
              <Text style={styles.priceLabel}>{strings.premium.priceLabel}</Text>
              <Text style={styles.price}>
                {priceString}
                <Text style={styles.perMonth}> {strings.premium.perMonth}</Text>
              </Text>
            </View>
            <TouchableOpacity
              style={styles.subscribeButton}
              onPress={handleSubscribe}
              disabled={purchasing}
            >
              {purchasing ? (
                <ActivityIndicator color="#0b0e14" />
              ) : (
                <Text style={styles.subscribeButtonText}>{strings.premium.subscribe}</Text>
              )}
            </TouchableOpacity>
          </View>
        )}

        <TouchableOpacity onPress={handleRestore} disabled={restoring} style={styles.restoreLink}>
          <Text style={styles.restoreLinkText}>
            {restoring ? strings.premium.subscribing : strings.premium.restore}
          </Text>
        </TouchableOpacity>

        <Text style={styles.legalNote}>{strings.premium.legalNote}</Text>
      </ScrollView>
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: theme.colors.background },
  header: { flexDirection: 'row', justifyContent: 'flex-end', paddingHorizontal: theme.spacing(4) },
  content: { padding: theme.spacing(4), paddingBottom: theme.spacing(10) },
  title: { fontSize: 26, fontWeight: '800', color: theme.colors.textPrimary, textAlign: 'center', marginTop: theme.spacing(2) },
  subtitle: {
    fontSize: 13.5,
    color: theme.colors.textSecondary,
    textAlign: 'center',
    marginTop: 8,
    marginBottom: theme.spacing(6),
    lineHeight: 19,
  },
  table: {
    backgroundColor: theme.colors.surface,
    borderWidth: 1,
    borderColor: theme.colors.border,
    borderRadius: theme.radius.lg,
    overflow: 'hidden',
    marginBottom: theme.spacing(5),
  },
  tableHeaderRow: {
    flexDirection: 'row',
    paddingVertical: 10,
    paddingHorizontal: 14,
    borderBottomWidth: 1,
    borderBottomColor: theme.colors.border,
  },
  tableHeaderCell: { flex: 1, fontSize: 11, fontWeight: '700', color: theme.colors.textMuted, textAlign: 'center' },
  tableRow: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 12,
    paddingHorizontal: 14,
    borderBottomWidth: 1,
    borderBottomColor: theme.colors.border,
  },
  featureCol: { flex: 2.4, textAlign: 'left' },
  featureLabel: { fontSize: 13, color: theme.colors.textPrimary },
  cell: { flex: 1, alignItems: 'center' },
  activeBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
    backgroundColor: 'rgba(74,222,128,0.1)',
    borderWidth: 1,
    borderColor: 'rgba(74,222,128,0.3)',
    borderRadius: theme.radius.md,
    paddingVertical: 14,
    marginBottom: theme.spacing(5),
  },
  activeBadgeText: { color: theme.colors.positive, fontWeight: '700', fontSize: 14 },
  priceCard: {
    backgroundColor: theme.colors.surface,
    borderWidth: 1,
    borderColor: theme.colors.border,
    borderRadius: theme.radius.lg,
    padding: theme.spacing(4),
    marginBottom: theme.spacing(4),
  },
  priceLabel: { fontSize: 12, color: theme.colors.textSecondary, marginBottom: 4 },
  price: { fontSize: 28, fontWeight: '800', color: theme.colors.textPrimary },
  perMonth: { fontSize: 14, fontWeight: '500', color: theme.colors.textSecondary },
  subscribeButton: {
    marginTop: theme.spacing(4),
    backgroundColor: theme.colors.accent,
    borderRadius: theme.radius.md,
    paddingVertical: 14,
    alignItems: 'center',
  },
  subscribeButtonText: { color: '#0b0e14', fontWeight: '700', fontSize: 15 },
  restoreLink: { alignItems: 'center', paddingVertical: 12 },
  restoreLinkText: { color: theme.colors.accent, fontSize: 13, fontWeight: '600' },
  legalNote: { fontSize: 11, color: theme.colors.textMuted, textAlign: 'center', lineHeight: 15, marginTop: theme.spacing(2) },
});
