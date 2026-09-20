import { useEffect, useState } from 'react';
import { View, Text, TextInput, Pressable, FlatList } from 'react-native';
import { useRouter } from 'expo-router';
import { UserPlus, X, User } from 'lucide-react-native';
import { Header } from '@/components/ui/header';
import { Friend, getFriends, addFriend, removeFriend } from '@/api/endpoints/friends.api';

export default function FriendsScreen() {
  const router = useRouter();
  const [email, setEmail] = useState('');
  const [friends, setFriends] = useState<Friend[]>([]);
  const [loading, setLoading] = useState(true);
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    loadFriends();
  }, []);

  async function loadFriends() {
    setLoading(true);
    setError(null);
    try {
      const data = await getFriends();
      setFriends(data);
    } catch (err) {
      console.error('Failed to load friends', err);
      setError('Could not load friends.');
    } finally {
      setLoading(false);
    }
  }

  async function handleAdd() {
    if (!email.trim()) return;
    setSending(true);
    try {
      await addFriend(email.trim());
      setEmail('');
      await loadFriends(); // refresh the real list from backend
    } catch (err) {
      console.error('Failed to add friend', err);
      setError('Could not add friend.');
    } finally {
      setSending(false);
    }
  }

  async function handleRemove(id: string) {
    // optimistic update
    const prev = friends;
    setFriends((cur) => cur.filter((f) => f.id !== id));
    try {
      await removeFriend(id);
    } catch (err) {
      console.error('Failed to remove friend', err);
      setError('Could not remove friend.');
      setFriends(prev); // revert on failure
    }
  }

  return (
    <View className="flex-1 bg-bg">
      <Header title="Friends" onBack={() => router.back()} showSettings={false} />

      <View className="p-6 pb-3">
        <View className="flex-row gap-2">
          <TextInput
            value={email}
            onChangeText={setEmail}
            placeholder="Enter email to add friend..."
            placeholderTextColor="#9CA3AF"
            autoCapitalize="none"
            keyboardType="email-address"
            className="flex-1 rounded-full border border-neutral-300 bg-white px-4 py-3 text-black"
          />
          <Pressable
            onPress={handleAdd}
            disabled={!email.trim() || sending}
            className={`h-12 w-12 items-center justify-center rounded-full ${email.trim() ? 'bg-violet-600' : 'bg-neutral-200'}`}
          >
            <UserPlus size={20} color="#fff" />
          </Pressable>
        </View>
        {error && (
          <Text className="mt-2 text-xs text-red-500">{error}</Text>
        )}
      </View>

      {loading ? (
        <Text className="mt-10 text-center text-sm text-neutral-400">
          Loading friends…
        </Text>
      ) : (
        <FlatList
          data={friends}
          keyExtractor={(item) => item.id}
          contentContainerClassName="gap-3 px-6 pb-6"
          renderItem={({ item }) => (
            <View className="flex-row items-center rounded-2xl bg-white p-4">
              <View className="mr-3 h-10 w-10 items-center justify-center rounded-full bg-violet-100">
                <User size={18} color="#7C5CFC" />
              </View>
              <View className="flex-1">
                <Text className="text-sm font-bold text-black">{item.name}</Text>
                <Text className="text-xs text-neutral-500">{item.email}</Text>
              </View>
              <Pressable onPress={() => handleRemove(item.id)}>
                <X size={20} color="#EF4444" />
              </Pressable>
            </View>
          )}
          ListEmptyComponent={
            <Text className="mt-10 text-center text-sm text-neutral-400">
              No friends yet — add someone by email above.
            </Text>
          }
        />
      )}
    </View>
  );
}