import { Ionicons } from '@expo/vector-icons';
import { Redirect } from 'expo-router';
import { useState } from 'react';
import {
  KeyboardAvoidingView,
  Platform,
  Pressable,
  Text,
  TextInput,
  View,
} from 'react-native';
import { useAuth } from '../context/AuthContext';

export default function LoginScreen() {
  const { signIn, signUp, token } = useAuth();
  const [mode, setMode] = useState<'signin' | 'signup'>('signin');

  const [fullName, setFullName] = useState('');
  const [contact, setContact] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (token) return <Redirect href="/(tabs)/home" />;

  async function handleSubmit() {
    setError(null);
    setLoading(true);
    try {
      if (mode === 'signin') {
        await signIn(email, password);
      } else {
        if (password !== confirmPassword) {
          setError('Passwords do not match.');
          setLoading(false);
          return;
        }
        await signUp(fullName, contact, email, password, confirmPassword);
      }
    } catch (e) {
      console.error('Auth error:', e);
      setError('Something went wrong. Try again.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <KeyboardAvoidingView
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      className="flex-1 bg-gray-50 items-center justify-center px-6"
    >
      <View className="w-full max-w-sm bg-white rounded-3xl p-6">
        <Text className="text-orange-500 font-semibold mb-1">
          Welcome to EduSathy
        </Text>
        <Text className="text-2xl font-bold text-gray-900 mb-1">
          Continue studying
        </Text>
        <Text className="text-gray-500 mb-5">
          Your summaries and questions are waiting.
        </Text>

        <View className="flex-row bg-gray-100 rounded-full p-1 mb-5">
          <Pressable
            onPress={() => setMode('signin')}
            className={`flex-1 py-2 rounded-full items-center ${mode === 'signin' ? 'bg-white ' : ''}`}
          >
            <Text
              className={
                mode === 'signin'
                  ? 'text-gray-900 font-semibold'
                  : 'text-gray-400'
              }
            >
              Sign in
            </Text>
          </Pressable>
          <Pressable
            onPress={() => setMode('signup')}
            className={`flex-1 py-2 rounded-full items-center ${mode === 'signup' ? 'bg-white' : ''}`}
          >
            <Text
              className={
                mode === 'signup'
                  ? 'text-gray-900 font-semibold'
                  : 'text-gray-400'
              }
            >
              Sign up
            </Text>
          </Pressable>
        </View>

        {mode === 'signup' && (
          <>
            <Text className="text-gray-800 font-medium mb-1.5">Full name</Text>
            <View className="flex-row items-center bg-gray-50 border border-gray-200 rounded-full px-4 py-3 mb-4">
              <Ionicons name="person-outline" size={18} color="#9CA3AF" />
              <TextInput
                value={fullName}
                onChangeText={setFullName}
                placeholder="Your full name"
                placeholderTextColor="#9CA3AF"
                className="flex-1 ml-2 text-gray-900"
              />
            </View>

            <Text className="text-gray-800 font-medium mb-1.5">
              Contact number
            </Text>
            <View className="flex-row items-center bg-gray-50 border border-gray-200 rounded-full px-4 py-3 mb-4">
              <Ionicons name="call-outline" size={18} color="#9CA3AF" />
              <TextInput
                value={contact}
                onChangeText={setContact}
                placeholder="98XXXXXXXX"
                placeholderTextColor="#9CA3AF"
                keyboardType="phone-pad"
                className="flex-1 ml-2 text-gray-900"
              />
            </View>
          </>
        )}

        <Text className="text-gray-800 font-medium mb-1.5">Email address</Text>
        <View className="flex-row items-center bg-gray-50 border border-gray-200 rounded-full px-4 py-3 mb-4">
          <Ionicons name="mail-outline" size={18} color="#9CA3AF" />
          <TextInput
            value={email}
            onChangeText={setEmail}
            placeholder="you@example.com"
            placeholderTextColor="#9CA3AF"
            autoCapitalize="none"
            keyboardType="email-address"
            className="flex-1 ml-2 text-gray-900"
          />
        </View>

        <Text className="text-gray-800 font-medium mb-1.5">Password</Text>
        <View className="flex-row items-center bg-gray-50 border border-gray-200 rounded-full px-4 py-3 mb-2">
          <Ionicons name="lock-closed-outline" size={18} color="#9CA3AF" />
          <TextInput
            value={password}
            onChangeText={setPassword}
            placeholder="At least 8 characters"
            placeholderTextColor="#9CA3AF"
            secureTextEntry={!showPassword}
            className="flex-1 ml-2 text-gray-900"
          />
          <Pressable onPress={() => setShowPassword(s => !s)}>
            <Ionicons
              name={showPassword ? 'eye-off-outline' : 'eye-outline'}
              size={18}
              color="#9CA3AF"
            />
          </Pressable>
        </View>

        {mode === 'signup' && (
          <>
            <Text className="text-gray-800 font-medium mb-1.5 mt-2">
              Confirm password
            </Text>
            <View className="flex-row items-center bg-gray-50 border border-gray-200 rounded-full px-4 py-3 mb-2">
              <Ionicons name="lock-closed-outline" size={18} color="#9CA3AF" />
              <TextInput
                value={confirmPassword}
                onChangeText={setConfirmPassword}
                placeholder="Re-enter password"
                placeholderTextColor="#9CA3AF"
                secureTextEntry={!showPassword}
                className="flex-1 ml-2 text-gray-900"
              />
            </View>
          </>
        )}

        {mode === 'signin' && (
          <Pressable className="self-end mb-4">
            <Text className="text-indigo-600 font-medium text-sm">
              Forgot password?
            </Text>
          </Pressable>
        )}

        {error && <Text className="text-red-500 text-sm mb-3">{error}</Text>}

        <Pressable
          onPress={handleSubmit}
          disabled={loading}
          className="bg-indigo-600 rounded-full py-4 items-center mb-4"
        >
          <Text className="text-white font-semibold">
            {loading
              ? 'Please wait...'
              : mode === 'signin'
                ? 'Sign in'
                : 'Sign up'}
          </Text>
        </Pressable>

        <Text className="text-center text-gray-400 text-xs">
          By continuing, you agree to use EduSathy responsibly for learning.
        </Text>
      </View>
    </KeyboardAvoidingView>
  );
}
