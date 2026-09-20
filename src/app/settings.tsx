import { useState } from 'react';
import { ScrollView, Text, TextInput, View, Pressable } from 'react-native';
import { useRouter } from 'expo-router';
import { LogOut } from 'lucide-react-native';
import { useAuth } from '@/context/AuthContext';
import { Header } from '@/components/ui/header';

export default function SettingsScreen() {
  const router = useRouter();
  const { user, signOut } = useAuth();

  const [name, setName] = useState(user?.name ?? '');
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [savingName, setSavingName] = useState(false);
  const [savingPassword, setSavingPassword] = useState(false);

  async function handleSaveName() {
    if (!name.trim()) return;
    setSavingName(true);
    try {
      // TODO: wire to backend, e.g. await api.patch('/user', { name });
    } finally {
      setSavingName(false);
    }
  }

  async function handleSavePassword() {
    if (!currentPassword || !newPassword) return;
    setSavingPassword(true);
    try {
      // TODO: wire to backend, e.g. await api.patch('/user/password', { currentPassword, newPassword });
      setCurrentPassword('');
      setNewPassword('');
    } finally {
      setSavingPassword(false);
    }
  }

  async function handleLogout() {
    await signOut();
    router.replace('/login');
  }

  return (
    <View className="flex-1 bg-bg">
      <Header title="Settings" onBack={() => router.back()} showSettings={false} />
      <ScrollView className="flex-1 p-6" contentContainerClassName="gap-4">
        <View className="gap-3 rounded-2xl bg-white p-5">
          <Text className="text-sm font-bold text-black">Change Name</Text>
          <TextInput
            value={name}
            onChangeText={setName}
            placeholder="Enter new name"
            placeholderTextColor="#9CA3AF"
            className="rounded-xl border border-neutral-300 px-4 py-3 text-black"
          />
          <Pressable
            className={`items-center rounded-full px-6 py-3 ${name.trim() ? 'bg-violet-600' : 'bg-neutral-200'}`}
            onPress={handleSaveName}
            disabled={!name.trim() || savingName}
          >
            <Text className="font-bold text-white">{savingName ? 'Saving…' : 'Save Name'}</Text>
          </Pressable>
        </View>

        <View className="gap-3 rounded-2xl bg-white p-5">
          <Text className="text-sm font-bold text-black">Change Password</Text>
          <TextInput
            value={currentPassword}
            onChangeText={setCurrentPassword}
            placeholder="Current password"
            placeholderTextColor="#9CA3AF"
            secureTextEntry
            className="rounded-xl border border-neutral-300 px-4 py-3 text-black"
          />
          <TextInput
            value={newPassword}
            onChangeText={setNewPassword}
            placeholder="New password"
            placeholderTextColor="#9CA3AF"
            secureTextEntry
            className="rounded-xl border border-neutral-300 px-4 py-3 text-black"
          />
          <Pressable
            className={`items-center rounded-full px-6 py-3 ${currentPassword && newPassword ? 'bg-violet-600' : 'bg-neutral-200'}`}
            onPress={handleSavePassword}
            disabled={!currentPassword || !newPassword || savingPassword}
          >
            <Text className="font-bold text-white">{savingPassword ? 'Saving…' : 'Save Password'}</Text>
          </Pressable>
        </View>

        <Pressable
          className="flex-row items-center justify-center gap-2 rounded-full border border-red-200 bg-white px-6 py-3"
          onPress={handleLogout}
        >
          <LogOut size={18} color="#EF4444" />
          <Text className="font-bold text-red-500">Logout</Text>
        </Pressable>
      </ScrollView>
    </View>
  );
}