'use client';

import { useState } from 'react';
import { User, Bell, Shield, Palette, Save, Loader2 } from 'lucide-react';
import { useToast } from '@/hooks/useToast';
import { useAuthStore } from '@/lib/store';
import { usersApi } from '@/lib/api';
import { PageHeader } from '@/components/PageHeader';
import { useTheme, type Theme } from '@/hooks/useTheme';

export default function SettingsPage() {
  const toast = useToast();
  const { user, updateUser } = useAuthStore();
  const [tab, setTab] = useState<'profile' | 'notifications' | 'security' | 'appearance'>('profile');
  const [toggles, setToggles] = useState({ email: true, push: true, weekly: false });
  // Shared with ThemeToggle in the header (src/hooks/useTheme.ts) so a
  // change made here or there stays in sync instead of each holding its
  // own out-of-sync local theme state.
  const { theme, setTheme } = useTheme();
  const [compact, setCompact] = useState(false);
  const [profile, setProfile] = useState({
    firstName: user?.firstName || '',
    lastName: user?.lastName || '',
    email: user?.email || '',
    phone: '',
  });
  const [savingProfile, setSavingProfile] = useState(false);
  const [passwordForm, setPasswordForm] = useState({ current: '', next: '', confirm: '' });
  const [changingPassword, setChangingPassword] = useState(false);

  const tabs = [
    { id: 'profile' as const, label: 'Profile', icon: User },
    { id: 'notifications' as const, label: 'Notifications', icon: Bell },
    { id: 'security' as const, label: 'Security', icon: Shield },
    { id: 'appearance' as const, label: 'Appearance', icon: Palette },
  ];

  const handleToggle = (key: keyof typeof toggles) => {
    setToggles(prev => ({ ...prev, [key]: !prev[key] }));
  };

  const handleSaveProfile = async () => {
    if (!user) return;
    setSavingProfile(true);
    try {
      await usersApi.update(user.id, {
        firstName: profile.firstName,
        lastName: profile.lastName,
        phone: profile.phone || undefined,
        isActive: true,
      });
      updateUser({
        firstName: profile.firstName,
        lastName: profile.lastName,
        fullName: `${profile.firstName} ${profile.lastName}`.trim(),
      });
      toast('success', 'Saved!', 'Profile settings updated');
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to update profile';
      toast('error', 'Error', msg);
    } finally {
      setSavingProfile(false);
    }
  };

  const handleSaveNotifications = () => {
    toast('success', 'Saved!', 'Notification settings updated for this session');
  };

  const handleChangePassword = async () => {
    if (!user) return;
    if (passwordForm.next !== passwordForm.confirm) {
      toast('error', 'Error', 'New password and confirmation do not match');
      return;
    }
    setChangingPassword(true);
    try {
      await usersApi.changePassword(user.id, {
        currentPassword: passwordForm.current,
        newPassword: passwordForm.next,
      });
      toast('success', 'Saved!', 'Password has been changed');
      setPasswordForm({ current: '', next: '', confirm: '' });
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to change password';
      toast('error', 'Error', msg);
    } finally {
      setChangingPassword(false);
    }
  };

  const handleThemeChange = (newTheme: Theme) => {
    setTheme(newTheme);
    toast('success', 'Saved!', 'Appearance settings updated');
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title="Settings"
        subtitle="Manage your account and preferences"
        breadcrumbs={[
          { label: 'Dashboard', href: '/dashboard' },
          { label: 'Settings' },
        ]}
      />

      <div className="flex gap-6">
        <div className="w-44 shrink-0">
          <nav className="space-y-0.5">
            {tabs.map(t => (
              <button
                key={t.id}
                onClick={() => setTab(t.id)}
                className={`w-full flex items-center gap-2 px-3 py-2 rounded-lg text-sm transition ${tab === t.id ? 'bg-primary/10 text-primary font-medium' : 'text-slate-600 dark:text-slate-400 hover:bg-surface-muted'}`}
              >
                <t.icon className="w-4 h-4" />{t.label}
              </button>
            ))}
          </nav>
        </div>

        <div className="flex-1 bg-surface rounded-xl border border-border-subtle p-6">
          {tab === 'profile' && (
            <div className="space-y-6">
              <h3 className="font-heading font-semibold text-foreground">Profile Information</h3>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">First Name</label>
                  <input type="text" value={profile.firstName} onChange={e => setProfile(p => ({ ...p, firstName: e.target.value }))} className="w-full px-3 py-2 border border-border-subtle rounded-lg bg-surface text-foreground text-sm focus:ring-2 focus:ring-primary focus:border-primary" />
                </div>
                <div>
                  <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Last Name</label>
                  <input type="text" value={profile.lastName} onChange={e => setProfile(p => ({ ...p, lastName: e.target.value }))} className="w-full px-3 py-2 border border-border-subtle rounded-lg bg-surface text-foreground text-sm focus:ring-2 focus:ring-primary focus:border-primary" />
                </div>
                <div>
                  <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Email</label>
                  <input type="email" value={profile.email} disabled title="Email cannot be changed here" className="w-full px-3 py-2 border border-border-subtle rounded-lg bg-surface-muted text-slate-500 text-sm cursor-not-allowed" />
                </div>
                <div>
                  <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Phone</label>
                  <input type="text" value={profile.phone} onChange={e => setProfile(p => ({ ...p, phone: e.target.value }))} placeholder="+62 xxx xxxx xxxx" className="w-full px-3 py-2 border border-border-subtle rounded-lg bg-surface text-foreground text-sm focus:ring-2 focus:ring-primary focus:border-primary" />
                </div>
              </div>
              <div className="flex justify-end pt-4 border-t border-border-subtle">
                <button onClick={handleSaveProfile} disabled={savingProfile} className="flex items-center gap-2 px-4 py-2 bg-primary hover:bg-primary/90 text-primary-foreground rounded-lg text-sm disabled:opacity-50">
                  {savingProfile ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}Save Changes
                </button>
              </div>
            </div>
          )}

          {tab === 'notifications' && (
            <div className="space-y-4">
              <h3 className="font-heading font-semibold text-foreground">Notification Preferences</h3>
              {[
                { key: 'email' as keyof typeof toggles, label: 'Email Notifications', desc: 'Receive notifications via email' },
                { key: 'push' as keyof typeof toggles, label: 'Push Notifications', desc: 'Browser push notifications' },
                { key: 'weekly' as keyof typeof toggles, label: 'Weekly Digest', desc: 'Summary of weekly activity' },
              ].map(item => (
                <div key={item.key} className="flex items-center justify-between py-3 border-b border-border-subtle last:border-0">
                  <div>
                    <p className="font-medium text-foreground">{item.label}</p>
                    <p className="text-sm text-slate-500">{item.desc}</p>
                  </div>
                  <button
                    onClick={() => handleToggle(item.key)}
                    aria-label={`${item.label}: ${toggles[item.key] ? 'enabled' : 'disabled'}`}
                    aria-pressed={toggles[item.key]}
                    className={`relative w-11 h-6 p-0 rounded-full transition ${toggles[item.key] ? 'bg-primary' : 'bg-slate-300 dark:bg-slate-600'}`}
                  >
                    <span className={`absolute left-0 top-0.5 w-5 h-5 bg-white rounded-full shadow transition-transform ${toggles[item.key] ? 'translate-x-5' : 'translate-x-0.5'}`} />
                  </button>
                </div>
              ))}
              <p className="text-xs text-slate-500">Notification preferences are stored for this session only - there is no server-side settings endpoint yet.</p>
              <div className="flex justify-end pt-4 border-t border-border-subtle">
                <button onClick={handleSaveNotifications} className="flex items-center gap-2 px-4 py-2 bg-primary hover:bg-primary/90 text-primary-foreground rounded-lg text-sm">
                  <Save className="w-4 h-4" />Save Preferences
                </button>
              </div>
            </div>
          )}

          {tab === 'security' && (
            <div className="space-y-4">
              <h3 className="font-heading font-semibold text-foreground">Security Settings</h3>
              <div className="p-4 bg-surface-muted rounded-lg space-y-3">
                <p className="font-medium text-foreground">Change Password</p>
                <input type="password" placeholder="Current password" value={passwordForm.current} onChange={e => setPasswordForm(p => ({ ...p, current: e.target.value }))} className="w-full px-3 py-2 border border-border-subtle rounded-lg bg-surface text-foreground text-sm focus:ring-2 focus:ring-primary focus:border-primary" />
                <input type="password" placeholder="New password" value={passwordForm.next} onChange={e => setPasswordForm(p => ({ ...p, next: e.target.value }))} className="w-full px-3 py-2 border border-border-subtle rounded-lg bg-surface text-foreground text-sm focus:ring-2 focus:ring-primary focus:border-primary" />
                <input type="password" placeholder="Confirm new password" value={passwordForm.confirm} onChange={e => setPasswordForm(p => ({ ...p, confirm: e.target.value }))} className="w-full px-3 py-2 border border-border-subtle rounded-lg bg-surface text-foreground text-sm focus:ring-2 focus:ring-primary focus:border-primary" />
                <div className="flex justify-end">
                  <button
                    onClick={handleChangePassword}
                    disabled={changingPassword || !passwordForm.current || !passwordForm.next}
                    className="flex items-center gap-2 px-3 py-1.5 bg-primary hover:bg-primary/90 text-primary-foreground rounded-lg text-sm disabled:opacity-50"
                  >
                    {changingPassword && <Loader2 className="w-3.5 h-3.5 animate-spin" />}Change Password
                  </button>
                </div>
              </div>
              <div className="p-4 bg-surface-muted rounded-lg">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-3">
                    <div className="w-10 h-10 rounded-lg bg-green-100 dark:bg-green-900/30 flex items-center justify-center">
                      <Shield className="w-5 h-5 text-green-600" />
                    </div>
                    <div>
                      <p className="font-medium text-foreground flex items-center gap-2">
                        Two-Factor Auth <span className="px-2 py-0.5 bg-green-100 text-green-700 text-xs rounded">Enabled</span>
                      </p>
                      <p className="text-sm text-slate-500">Extra layer of security</p>
                    </div>
                  </div>
                  <button disabled title="Not available yet - backend API pending" className="px-3 py-1.5 border border-border-subtle text-red-300 dark:text-red-800 rounded-lg text-sm cursor-not-allowed">Disable</button>
                </div>
              </div>
              <div className="p-4 bg-surface-muted rounded-lg">
                <div className="flex items-center justify-between">
                  <div>
                    <p className="font-medium text-foreground">Active Sessions</p>
                    <p className="text-sm text-slate-500">1 active session</p>
                  </div>
                  <button disabled title="Not available yet - backend API pending" className="px-3 py-1.5 border border-border-subtle text-red-300 dark:text-red-800 rounded-lg text-sm cursor-not-allowed">Sign out all</button>
                </div>
              </div>
            </div>
          )}

          {tab === 'appearance' && (
            <div className="space-y-6">
              <h3 className="font-heading font-semibold text-foreground">Appearance</h3>
              <div>
                <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-3">Theme</label>
                <div className="flex gap-3">
                  {[
                    { value: 'light' as Theme, label: 'Light', bg: 'bg-white', border: 'border-slate-300' },
                    { value: 'dark' as Theme, label: 'Dark', bg: 'bg-slate-800', border: 'border-slate-600' },
                    { value: 'system' as Theme, label: 'System', bg: 'bg-gradient-to-r from-white to-slate-800', border: 'border-slate-300' },
                  ].map(t => (
                    <button
                      key={t.value}
                      onClick={() => handleThemeChange(t.value)}
                      aria-label={`Select ${t.label} theme`}
                      aria-pressed={theme === t.value}
                      className={`flex-1 p-4 rounded-xl border-2 transition ${theme === t.value ? 'border-primary' : 'border-transparent ' + t.border}`}
                    >
                      <div className={`w-full h-12 rounded-lg mb-2 ${t.bg} border ${t.border}`} />
                      <p className="text-sm font-medium text-slate-700 dark:text-slate-300">{t.label}</p>
                    </button>
                  ))}
                </div>
              </div>
              <div className="flex items-center justify-between py-3 border-t border-border-subtle">
                <div>
                  <p className="font-medium text-foreground">Compact Mode</p>
                  <p className="text-sm text-slate-500">Reduce spacing for denser UI</p>
                </div>
                <button
                  onClick={() => setCompact(c => !c)}
                  aria-label={`Compact mode: ${compact ? 'enabled' : 'disabled'}`}
                  aria-pressed={compact}
                  className={`relative w-11 h-6 p-0 rounded-full transition ${compact ? 'bg-primary' : 'bg-slate-300 dark:bg-slate-600'}`}
                >
                  <span className={`absolute left-0 top-0.5 w-5 h-5 bg-white rounded-full shadow transition-transform ${compact ? 'translate-x-5' : 'translate-x-0.5'}`} />
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
