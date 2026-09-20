import { View, Text, Pressable } from 'react-native';
import { Settings, ChevronLeft } from 'lucide-react-native';
import { useRouter } from 'expo-router';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

export function Header({
  title = 'EduSathy',
  onBack,
  showSettings = true,
}: {
  title?: string;
  onBack?: () => void;
  showSettings?: boolean;
}) {
  const router = useRouter();
  const insets = useSafeAreaInsets();

  return (
    <View
      style={{ backgroundColor: '#E4DCFA', paddingTop: insets.top + 12 }}
      className="flex-row items-center justify-between px-6 pb-4"
    >
      <View className="flex-row items-center gap-2">
        {onBack && (
          <Pressable onPress={onBack} className="h-9 w-9 items-center justify-center">
            <ChevronLeft size={24} color="#374151" />
          </Pressable>
        )}
        <Text className="text-3xl font-bold text-neutral-900">{title}</Text>
      </View>
      {showSettings && (
        <Pressable
          onPress={() => router.push('/settings')}
          className="h-12 w-12 items-center justify-center rounded-full bg-white"
        >
          <Settings size={22} color="#374151" />
        </Pressable>
      )}
    </View>
  );
}