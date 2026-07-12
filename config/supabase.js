import 'react-native-url-polyfill/auto';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { createClient } from '@supabase/supabase-js';

// La URL y la clave "publishable"/anon son seguras para incluir en el
// cliente — el acceso real a los datos está protegido por RLS en la base.
const SUPABASE_URL = 'https://nvotoypwygvhbametjzu.supabase.co';
const SUPABASE_ANON_KEY =
  'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Im52b3RveXB3eWd2aGJhbWV0anp1Iiwicm9sZSI6ImFub24iLCJpYXQiOjE3ODM4NjA0MTcsImV4cCI6MjA5OTQzNjQxN30.KxHupKECuIk8D-jFP-7FlETYNBKr7ZNlNhdIiHtYXgg';

export const supabase = createClient(SUPABASE_URL, SUPABASE_ANON_KEY, {
  auth: {
    storage: AsyncStorage,
    autoRefreshToken: true,
    persistSession: true,
    detectSessionInUrl: false,
  },
});
