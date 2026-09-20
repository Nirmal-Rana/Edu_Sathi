import { useEffect, useState } from 'react';
import { Ionicons } from '@expo/vector-icons';
import { ScrollView, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { getHistory, HistoryDoc } from '@/api/endpoints/history.api';

export default function HistoryScreen() {
  const [history, setHistory] = useState<HistoryDoc[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    loadHistory();
  }, []);

  async function loadHistory() {
    setLoading(true);
    setError(null);
    try {
      const data = await getHistory();
      setHistory(data);
    } catch (err) {
      console.error('Failed to load history', err);
      setError('Could not load history.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <SafeAreaView className="flex-1 bg-bg">
      <ScrollView contentContainerClassName="p-5 pb-10">
        {loading ? (
          <Text className="text-neutral-400 text-sm">Loading history…</Text>
        ) : error ? (
          <Text className="text-red-500 text-sm">{error}</Text>
        ) : history.length === 0 ? (
          <Text className="mt-10 text-center text-sm text-neutral-400">
            No quiz history yet — take a quiz to see your results here.
          </Text>
        ) : (
          <View className="gap-4">
            {history.map(item => (
              <View
                key={item.id}
                className={`bg-white rounded-2xl p-4 ${item.isCurrent ? 'border-2 border-accent' : ''}`}
              >
                <View className="flex-row items-start mb-3">
                  <View className="w-9 h-9 rounded-lg bg-orange-50 items-center justify-center mr-3">
                    <Ionicons name="document-text" size={18} color="#F97316" />
                  </View>
                  <View className="flex-1">
                    <View className="flex-row items-center gap-2">
                      <Text
                        className="text-sm font-bold text-gray-900 flex-1"
                        numberOfLines={1}
                      >
                        {item.name}
                      </Text>
                    </View>
                    <Text className="text-xs text-gray-400 mt-0.5">
                      {item.date} · {item.pages} pages · {item.sizeMB} MB
                    </Text>
                  </View>
                </View>
              </View>
            ))}
          </View>
        )}
      </ScrollView>
    </SafeAreaView>
  );
}