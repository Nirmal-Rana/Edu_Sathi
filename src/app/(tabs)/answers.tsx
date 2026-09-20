import { useEffect, useRef, useState } from 'react';
import { Ionicons } from '@expo/vector-icons';
import { useLocalSearchParams } from 'expo-router';
import { ScrollView, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { getDocumentSummary, DocumentSummary } from '@/api/endpoints/documents.api';

export default function AnswersScreen() {
  const { documentId } = useLocalSearchParams<{ documentId?: string }>();
  const [summary, setSummary] = useState<DocumentSummary | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);

  useEffect(() => {
    if (!documentId) {
      setLoading(false);
      setError('No document selected.');
      return;
    }

    fetchSummary(true);

    return () => {
      if (pollRef.current) clearInterval(pollRef.current);
    };
  }, [documentId]);

  async function fetchSummary(isInitial = false) {
    if (isInitial) setLoading(true);
    setError(null);
    try {
      const result = await getDocumentSummary(Number(documentId));
      if (result) {
        setSummary(result);
        if (pollRef.current) {
          clearInterval(pollRef.current);
          pollRef.current = null;
        }
      } else if (!pollRef.current) {
        // not ready yet — poll every few seconds until it is
        pollRef.current = setInterval(() => fetchSummary(false), 4000);
      }
    } catch (err) {
      console.error('Failed to load summary', err);
      setError('Could not load summary.');
      if (pollRef.current) {
        clearInterval(pollRef.current);
        pollRef.current = null;
      }
    } finally {
      if (isInitial) setLoading(false);
    }
  }

  return (
    <SafeAreaView className="flex-1 bg-bg">
      <ScrollView contentContainerClassName="px-5 pb-10">
        {loading ? (
          <Text className="text-neutral-400 text-sm mt-10 text-center">
            Loading summary…
          </Text>
        ) : error ? (
          <Text className="text-red-500 text-sm mt-10 text-center">{error}</Text>
        ) : !summary ? (
          <Text className="text-neutral-400 text-sm mt-10 text-center">
            Still generating your summary — this can take a moment for larger
            documents.
          </Text>
        ) : (
          <>
            <View className="bg-white rounded-2xl p-4 mb-4">
              <View className="flex-row items-center justify-between mb-3" />
              <Text className="text-base font-bold text-gray-900 mb-2">
                Overview
              </Text>
              {summary.overview.map((point, i) => (
                <View key={i} className="flex-row mb-2">
                  <Text className="text-primary mr-2">•</Text>
                  <Text className="text-gray-700 text-sm flex-1 leading-5">
                    {point}
                  </Text>
                </View>
              ))}
            </View>

            <View className="bg-gray-50 rounded-2xl p-4 mb-5">
              <View className="flex-row items-center gap-1.5 mb-3">
                <Ionicons name="bulb-outline" size={16} color="#F97316" />
                <Text className="text-base font-bold text-gray-900">
                  Key Takeaways
                </Text>
              </View>
              <View className="gap-3">
                {summary.keyTakeaways.map((kt, i) => (
                  <View
                    key={i}
                    className="bg-white rounded-lg p-3 border-l-4 border-accent"
                  >
                    <Text className="text-[10px] font-bold text-gray-400 tracking-wide mb-1">
                      {kt.label}
                    </Text>
                    <Text className="text-sm text-gray-800">{kt.value}</Text>
                  </View>
                ))}
              </View>
            </View>
          </>
        )}
      </ScrollView>
    </SafeAreaView>
  );
}