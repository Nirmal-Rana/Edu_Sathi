import { Ionicons } from '@expo/vector-icons';
import * as DocumentPicker from 'expo-document-picker';
import { useRouter } from 'expo-router';
import { useEffect, useState } from 'react';
import { Pressable, ScrollView, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { uploadDocument, getDocuments, DocumentDto } from '@/api/endpoints/documents.api';

export default function HomeScreen() {
  const router = useRouter();
  const [pendingFiles, setPendingFiles] = useState<{ name: string; uri: string }[]>([]);
  const [documents, setDocuments] = useState<DocumentDto[]>([]);
  const [uploading, setUploading] = useState(false);
  const [loadingDocs, setLoadingDocs] = useState(true);

  useEffect(() => {
    loadDocuments();
  }, []);

  // Poll while any document is still pending/processing, so "Processing..."
  // resolves automatically once the backend finishes and reports a real
  // status — no manual refresh needed. Stops itself once nothing is pending.
  useEffect(() => {
    const stillWorking = documents.some(
      d => d.status === 'pending' || d.status === 'processing' || (!d.status && d.pages === 0)
    );
    if (!stillWorking) return;

    const interval = setInterval(() => {
      loadDocuments();
    }, 3000);

    return () => clearInterval(interval);
  }, [documents]);

  async function loadDocuments() {
    setLoadingDocs(true);
    try {
      const docs = await getDocuments();
      setDocuments(docs);
    } catch (err) {
      console.error('Failed to load documents', err);
    } finally {
      setLoadingDocs(false);
    }
  }

  async function handleBrowse() {
    const result = await DocumentPicker.getDocumentAsync({
      type: 'application/pdf',
      multiple: true,
    });
    if (result.canceled) return;
    setPendingFiles(prev => [
      ...prev,
      ...result.assets.map(a => ({ name: a.name, uri: a.uri })),
    ]);
  }

  function removePending(name: string) {
    setPendingFiles(prev => prev.filter(f => f.name !== name));
  }

  async function handleSubmit() {
    if (pendingFiles.length === 0) return;
    setUploading(true);

    for (const f of pendingFiles) {
      try {
        await uploadDocument(f.uri, f.name);
      } catch (err) {
        console.error('Upload failed for', f.name, err);
      }
    }

    setPendingFiles([]);
    setUploading(false);
    await loadDocuments(); // refresh the real list from backend
  }

  function openDocument(doc: DocumentDto) {
    if (doc.status !== 'completed') return; // not ready to view yet
    router.push({ pathname: '/answers', params: { documentId: String(doc.id) } });
  }

  return (
    <SafeAreaView className="flex-1 bg-bg">
      <ScrollView contentContainerClassName="px-5 pb-5">
        <Pressable
          onPress={() => router.push('/friends')}
          className="flex-row items-center justify-between bg-white rounded-2xl p-4 mb-4"
        >
          <View className="flex-row items-center">
            <View className="w-10 h-10 rounded-full bg-violet-100 items-center justify-center mr-3">
              <Ionicons name="people" size={18} color="#7C5CFC" />
            </View>
            <Text className="text-sm font-bold text-gray-900">Friends</Text>
          </View>
          <Ionicons name="chevron-forward" size={18} color="#9CA3AF" />
        </Pressable>

        <View className="bg-white rounded-2xl p-4 mb-4">
          <Text className="text-base font-bold text-gray-900">
            Upload a PDF
          </Text>
          <Text className="text-gray-500 text-xs mt-0.5 mb-3.5">
            Lecture notes, textbook chapters, or past papers — anything you need
            to master.
          </Text>

          <Pressable
            onPress={handleBrowse}
            className="border border-dashed border-gray-200 rounded-xl py-7 items-center bg-gray-50"
          >
            <View className="w-10 h-10 rounded-full bg-primary items-center justify-center mb-2.5">
              <Ionicons name="cloud-upload-outline" size={22} color="#fff" />
            </View>
            <Text className="text-sm text-gray-900 text-center">
              Drag & drop your PDFs here, or{' '}
              <Text className="text-accent font-semibold">browse</Text>
            </Text>
            <Text className="text-[11px] text-gray-400 mt-1">
              PDF only · up to 20 MB each
            </Text>
          </Pressable>

          {pendingFiles.length > 0 && (
            <View className="mt-3 gap-2">
              {pendingFiles.map(f => (
                <View
                  key={f.name}
                  className="flex-row items-center justify-between bg-gray-50 rounded-lg px-3 py-2"
                >
                  <View className="flex-row items-center flex-1">
                    <Ionicons
                      name="document-outline"
                      size={16}
                      color="#6B7280"
                    />
                    <Text
                      className="text-xs text-gray-700 ml-2 flex-1"
                      numberOfLines={1}
                    >
                      {f.name}
                    </Text>
                  </View>
                  <Pressable onPress={() => removePending(f.name)}>
                    <Ionicons name="close-circle" size={18} color="#9CA3AF" />
                  </Pressable>
                </View>
              ))}
            </View>
          )}

          <View className="flex-row justify-end mt-3.5 gap-2.5">
            <Pressable
              className="px-5 py-2"
              onPress={() => setPendingFiles([])}
            >
              <Text className="text-gray-500 font-semibold text-sm">
                Cancel
              </Text>
            </Pressable>
            <Pressable
              className={`px-5 py-2 rounded-lg ${pendingFiles.length ? 'bg-primary' : 'bg-gray-200'}`}
              onPress={handleSubmit}
              disabled={pendingFiles.length === 0 || uploading}
            >
              <Text className="text-white font-semibold text-sm">
                {uploading
                  ? 'Uploading…'
                  : `Submit${pendingFiles.length > 1 ? ` (${pendingFiles.length})` : ''}`}
              </Text>
            </Pressable>
          </View>
        </View>

        <Text className="text-neutral-900 font-bold text-sm mb-2.5">
          Current Documents {documents.length > 0 && `(${documents.length})`}
        </Text>
        <View className="gap-3 mb-4">
          {loadingDocs ? (
            <Text className="text-neutral-400 text-sm">Loading documents…</Text>
          ) : (
            documents.map(doc => (
              <Pressable
                key={doc.id}
                onPress={() => openDocument(doc)}
                className="flex-row items-center bg-white rounded-2xl p-3.5"
              >
                <View className="w-10 h-10 rounded-xl bg-orange-50 items-center justify-center mr-3">
                  <Ionicons name="document-text" size={20} color="#F97316" />
                </View>
                <View className="flex-1">
                  <Text
                    className="text-sm font-bold text-gray-900"
                    numberOfLines={1}
                  >
                    {doc.name}
                  </Text>
                </View>
                <Text className="text-xs text-gray-400">
                  {doc.status === 'completed'
                    ? `${doc.pages} pages`
                    : doc.status === 'failed'
                    ? 'Failed'
                    : 'Processing...'}
                </Text>
                {doc.status === 'completed' && (
                  <Ionicons name="chevron-forward" size={16} color="#9CA3AF" style={{ marginLeft: 6 }} />
                )}
              </Pressable>
            ))
          )}
        </View>

        <View className="flex-row gap-3 mb-4">
          <View className="flex-1 bg-white rounded-2xl p-3.5">
            <View className="w-8 h-8 rounded-lg bg-violet-100 items-center justify-center mb-2.5">
              <Ionicons name="book-outline" size={18} color="#7C5CFC" />
            </View>
            <Text className="text-sm font-bold text-gray-900 mb-1">
              Smart summaries
            </Text>
            <Text className="text-[11px] text-gray-500 leading-4">
              Key points and takeaways extracted in seconds.
            </Text>
          </View>
          <View className="flex-1 bg-white rounded-2xl p-3.5">
            <View className="w-8 h-8 rounded-lg bg-orange-100 items-center justify-center mb-2.5">
              <Ionicons name="list-outline" size={18} color="#F97316" />
            </View>
            <Text className="text-sm font-bold text-gray-900 mb-1">
              Adaptive practice
            </Text>
            <Text className="text-[11px] text-gray-500 leading-4">
              Easy, medium and hard questions built from your file.
            </Text>
          </View>
        </View>
      </ScrollView>
    </SafeAreaView>
  );
}