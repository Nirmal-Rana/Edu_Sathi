import { Ionicons } from '@expo/vector-icons';
import { Redirect, Tabs } from 'expo-router';
import { Header } from '../../components/ui/header';
import { useAuth } from '../../context/AuthContext';

const ICONS: Record<string, keyof typeof Ionicons.glyphMap> = {
  home: 'home',
  history: 'time',
  answers: 'checkmark-done-outline',
  questionnaire: 'help-circle-outline',
  profile: 'person',
};

const TITLES: Record<string, string> = {
  home: 'EduSathy',
  history: 'History',
  answers: 'Answers',
  questionnaire: 'Questionnaire',
  profile: 'Profile',
};

export default function TabLayout() {
  const { token } = useAuth();
  if (!token) return <Redirect href="/login" />;

  return (
    <Tabs
      screenOptions={({ route }) => ({
        header: () => <Header title={TITLES[route.name]} />,
        tabBarActiveTintColor: '#7C5CFC',
        tabBarInactiveTintColor: '#9CA3AF',
        tabBarStyle: {
          backgroundColor: '#F3F0FF',
          borderTopWidth: 0,
        },
        tabBarIcon: ({ color, size }) => (
          <Ionicons name={ICONS[route.name]} size={size} color={color} />
        ),
      })}
    >
      <Tabs.Screen name="home" options={{ title: 'Home' }} />
      <Tabs.Screen name="answers" options={{ title: 'Answers' }} />
      <Tabs.Screen name="questionnaire" options={{ title: 'Questionnaire' }} />
      <Tabs.Screen name="history" options={{ title: 'History' }} />
    </Tabs>
  );
}
