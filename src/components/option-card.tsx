import { Pressable, Text, View } from 'react-native';
import { ReactNode } from 'react';

type OptionCardProps = {
  icon: ReactNode;
  title: string;
  subtitle: string;
  onPress: () => void;
};

export function OptionCard({ icon, title, subtitle, onPress }: OptionCardProps) {
  return (
    <Pressable
      onPress={onPress}
      className="w-full flex-row items-start gap-4 rounded-2xl bg-white p-5 active:opacity-80"
    >
      <View className="h-10 w-10 items-center justify-center rounded-full bg-orange-100">
        {icon}
      </View>
      <View className="flex-1">
        <Text className="text-base font-bold text-black">{title}</Text>
        <Text className="mt-1 text-sm text-neutral-500">{subtitle}</Text>
      </View>
    </Pressable>
  );
}