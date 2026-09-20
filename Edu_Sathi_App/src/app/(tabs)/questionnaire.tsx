import { useEffect, useState } from 'react';
import { FlatList, Pressable, ScrollView, Text, TextInput, View } from 'react-native';
import { useNavigation } from 'expo-router';
import { DoorOpen, FileUp, FolderOpen, Plus, X } from 'lucide-react-native';
import * as DocumentPicker from 'expo-document-picker';
import { DUMMY_FRIENDS, Friend } from '@/data/dummy/friends.dummy';
import { OptionCard } from '@/components/option-card';
import { Header } from '@/components/ui/header';
import { useQuestionnaireFlow } from '@/hooks/use-questionnaire-flow';
import { SavedFile } from '@/data/dummy/files.dummy';

export default function QuestionnaireScreen() {
  const {
    step, mode, savedFiles, quiz, loading, error,
    pickMode, goToUpload, goToSaved, confirmUpload, chooseSavedFile, goBack,
    joinRoom,
  } = useQuestionnaireFlow();

  const navigation = useNavigation();
  const [roomCode, setRoomCode] = useState('');
  const [pendingFiles, setPendingFiles] = useState<{ name: string; uri: string }[]>([]);
  const [friends] = useState<Friend[]>(DUMMY_FRIENDS);

  const title =
    step === 'mode' ? 'Questionnaire'
    : step === 'source' ? (mode === 'solo' ? 'Solo' : 'Create Custom')
    : step === 'upload' ? 'Upload File'
    : step === 'saved' ? 'Saved Files'
    : 'Questionnaire';

  useEffect(() => {
    navigation.setOptions({
      header: () =>
        step !== 'mode' ? (
          <Header title={title} onBack={goBack} />
        ) : (
          <Header title="Questionnaire" />
        ),
    });
  }, [step, title]);

  async function handleBrowse() {
    const result = await DocumentPicker.getDocumentAsync({
      type: 'application/pdf',
      multiple: true,
    });
    if (result.canceled) return;
    setPendingFiles((prev) => [
      ...prev,
      ...result.assets.map((a) => ({ name: a.name, uri: a.uri })),
    ]);
  }

  function removePending(name: string) {
    setPendingFiles((prev) => prev.filter((f) => f.name !== name));
  }

  function handleSubmitUpload() {
    if (pendingFiles.length === 0) return;
    // For now, confirm the first picked file's uri — adjust if confirmUpload should handle multiple
    confirmUpload(pendingFiles[0].uri);
    setPendingFiles([]);
  }

  function handleInvite(friendId: string) {
    // TODO: wire to backend invite endpoint, e.g. inviteFriendToRoom(roomCode, friendId)
    console.log('Invite sent to', friendId);
  }

  return (
    <ScrollView className="flex-1 bg-bg p-6" contentContainerClassName="gap-4">
      {error && <Text className="text-red-500">{error}</Text>}

      {step === 'mode' && (
        <View className="gap-4">
          <View className="items-center gap-3 rounded-3xl border border-neutral-200 bg-white p-8">
            <View className="h-16 w-16 items-center justify-center rounded-full border-2 border-blue-500">
              <DoorOpen size={28} color="#3B82F6" />
            </View>
            <Text className="text-xl font-bold text-black">Join a Room</Text>
            <Text className="text-center text-sm text-neutral-500">
              Enter a shareable code given by your peer to join a live room and compete in real-time.
            </Text>
            <View className="mt-2 w-full flex-row gap-2">
              <TextInput
                value={roomCode}
                onChangeText={setRoomCode}
                placeholder="Enter Room Code..."
                placeholderTextColor="#9CA3AF"
                autoCapitalize="characters"
                className="flex-1 rounded-full border border-neutral-300 px-4 py-3 text-black"
              />
              <Pressable
                className="items-center justify-center rounded-full bg-blue-600 px-6"
                onPress={() => joinRoom(roomCode)}
              >
                <Text className="font-bold text-white">Join</Text>
              </Pressable>
            </View>
          </View>

          <View className="items-center gap-3 rounded-3xl border border-neutral-200 bg-white p-8">
            <View className="h-16 w-16 items-center justify-center rounded-full border-2 border-green-500">
              <Plus size={28} color="#22C55E" />
            </View>
            <Text className="text-xl font-bold text-black">Create Custom</Text>
            <Text className="text-center text-sm text-neutral-500">
              Upload multiple PDFs, pick categories like Solo or Global, and share custom quiz rooms with others.
            </Text>
            <Pressable
              className="mt-2 rounded-full bg-green-600 px-8 py-3"
              onPress={() => pickMode('custom')}
            >
              <Text className="font-bold text-white">Create Now</Text>
            </Pressable>
          </View>
        </View>
      )}

      {step === 'source' && (
        <View className="gap-3">
          <OptionCard
            icon={<FileUp size={20} />}
            title="Upload File"
            subtitle="Add a new PDF from your device."
            onPress={goToUpload}
          />
          <OptionCard
            icon={<FolderOpen size={20} />}
            title="Saved Files"
            subtitle="Reuse a document you've already uploaded."
            onPress={goToSaved}
          />
        </View>
      )}

      {step === 'upload' && (
        <View className="gap-3 rounded-2xl bg-white p-6">
          <Pressable
            onPress={handleBrowse}
            className="items-center rounded-xl border border-dashed border-neutral-200 bg-neutral-50 py-7"
          >
            <Text className="text-black">
              Drag & drop your PDF here, or <Text className="font-semibold text-orange-500">browse</Text>
            </Text>
            <Text className="mt-1 text-xs text-neutral-400">PDF only · up to 20 MB</Text>
          </Pressable>

          {pendingFiles.length > 0 && (
            <View className="gap-2">
              {pendingFiles.map((f) => (
                <View
                  key={f.name}
                  className="flex-row items-center justify-between rounded-lg bg-neutral-50 px-3 py-2"
                >
                  <Text className="mr-2 flex-1 text-xs text-neutral-700" numberOfLines={1}>
                    {f.name}
                  </Text>
                  <Pressable onPress={() => removePending(f.name)}>
                    <X size={16} color="#9CA3AF" />
                  </Pressable>
                </View>
              ))}
            </View>
          )}

          <Pressable
            className={`items-center rounded-full px-6 py-3 ${pendingFiles.length ? 'bg-violet-600' : 'bg-neutral-200'}`}
            onPress={handleSubmitUpload}
            disabled={pendingFiles.length === 0}
          >
            <Text className="font-bold text-white">
              Confirm{pendingFiles.length > 1 ? ` (${pendingFiles.length})` : ''}
            </Text>
          </Pressable>
        </View>
      )}

      {step === 'saved' && (
        <FlatList
          data={savedFiles}
          keyExtractor={(item) => item.id}
          contentContainerClassName="gap-3"
          renderItem={({ item }: { item: SavedFile }) => (
            <Pressable
              className="rounded-2xl bg-white p-4"
              onPress={() => chooseSavedFile(item)}
            >
              <Text className="font-bold text-black">{item.name}</Text>
              <Text className="text-xs text-neutral-500">
                {item.pages} pages · {item.sizeKb} KB
              </Text>
            </Pressable>
          )}
        />
      )}

      {step === 'quiz' && (
        <View className="gap-4">
          {quiz.map((q, i) => (
            <View key={q.id} className="gap-3 rounded-2xl bg-white p-5">
              <Text className="font-bold text-black">
                {i + 1}. {q.prompt}
              </Text>
              {q.options.map((opt) => (
                <View key={opt.id} className="rounded-xl border border-neutral-200 p-3">
                  <Text className="text-black">
                    {opt.label}  {opt.text}
                  </Text>
                </View>
              ))}
            </View>
          ))}
        </View>
      )}

      {step === 'share' && (
        <View className="gap-4">
          <View className="items-center gap-2 rounded-3xl bg-white p-6">
            <Text className="text-xs font-semibold tracking-widest text-neutral-400">
              YOUR QUESTIONNAIRE CODE
            </Text>
            <Text className="text-3xl font-bold tracking-[6px] text-orange-500">
              L2TV9R
            </Text>
            <Text className="text-xs text-neutral-500">
              10 medium questions · Introduction to Photosynthesis.pdf
            </Text>
            <View className="mt-3 flex-row gap-3">
              <Pressable className="rounded-full border border-neutral-300 px-5 py-2">
                <Text className="font-semibold text-black">Copy code</Text>
              </Pressable>
              <Pressable className="rounded-full bg-violet-600 px-5 py-2">
                <Text className="font-semibold text-white">Start it myself</Text>
              </Pressable>
            </View>
          </View>

          <View className="rounded-3xl bg-white p-5">
            <Text className="mb-3 text-sm font-bold text-black">Invite Friends</Text>
            <View className="gap-2">
              {friends.map((friend) => (
                <View
                  key={friend.id}
                  className="flex-row items-center justify-between rounded-xl bg-neutral-50 px-4 py-3"
                >
                  <Text className="text-sm text-black">{friend.name}</Text>
                  <Pressable
                    className="rounded-full bg-violet-600 px-4 py-1.5"
                    onPress={() => handleInvite(friend.id)}
                  >
                    <Text className="text-xs font-semibold text-white">Add</Text>
                  </Pressable>
                </View>
              ))}
            </View>
          </View>
        </View>
      )}

      {loading && <Text className="text-neutral-900">Loading…</Text>}
    </ScrollView>
  );
}