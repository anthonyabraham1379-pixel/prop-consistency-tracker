import React, { createContext, useContext, useEffect, useState } from 'react';
import { Platform } from 'react-native';
import Purchases from 'react-native-purchases';
import { REVENUECAT_API_KEY_ANDROID, ENTITLEMENT_ID } from '../config/premium';
import { useAuth } from './AuthContext';

const PremiumContext = createContext(null);

function isEntitledFrom(customerInfo) {
  return !!customerInfo?.entitlements?.active?.[ENTITLEMENT_ID];
}

export function PremiumProvider({ children }) {
  const { user } = useAuth();
  const [isPremium, setIsPremium] = useState(false);
  const [loading, setLoading] = useState(true);
  const [offerings, setOfferings] = useState(null);

  useEffect(() => {
    if (Platform.OS !== 'android') {
      setLoading(false);
      return;
    }
    Purchases.configure({ apiKey: REVENUECAT_API_KEY_ANDROID });

    Purchases.getCustomerInfo()
      .then((info) => setIsPremium(isEntitledFrom(info)))
      .catch(() => {})
      .finally(() => setLoading(false));

    Purchases.getOfferings()
      .then((result) => setOfferings(result))
      .catch(() => {});

    const listener = (info) => setIsPremium(isEntitledFrom(info));
    Purchases.addCustomerInfoUpdateListener(listener);
    return () => Purchases.removeCustomerInfoUpdateListener(listener);
  }, []);

  useEffect(() => {
    if (Platform.OS !== 'android') return;
    if (user) {
      Purchases.logIn(user.id).catch(() => {});
    } else {
      Purchases.logOut().catch(() => {});
    }
  }, [user]);

  const purchasePackage = async (pkg) => {
    const { customerInfo } = await Purchases.purchasePackage(pkg);
    setIsPremium(isEntitledFrom(customerInfo));
    return customerInfo;
  };

  const restorePurchases = async () => {
    const customerInfo = await Purchases.restorePurchases();
    setIsPremium(isEntitledFrom(customerInfo));
    return customerInfo;
  };

  const value = { isPremium, loading, offerings, purchasePackage, restorePurchases };

  return <PremiumContext.Provider value={value}>{children}</PremiumContext.Provider>;
}

export function usePremium() {
  const ctx = useContext(PremiumContext);
  if (!ctx) throw new Error('usePremium debe usarse dentro de PremiumProvider');
  return ctx;
}
