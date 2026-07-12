import React from 'react';
import { Platform, StyleSheet, View } from 'react-native';
import { BannerAd, BannerAdSize, TestIds } from 'react-native-google-mobile-ads';
import { usePremium } from '../context/PremiumContext';

// TestIds.BANNER es el Ad Unit ID de prueba oficial de Google. Reemplazar por
// el ID real una vez creada la cuenta de AdMob, sin tocar el resto del código.
export default function AdBanner() {
  const { isPremium } = usePremium();

  if (isPremium || Platform.OS !== 'android') return null;

  return (
    <View style={styles.wrap}>
      <BannerAd unitId={TestIds.BANNER} size={BannerAdSize.BANNER} />
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: { alignItems: 'center', marginTop: 12 },
});
