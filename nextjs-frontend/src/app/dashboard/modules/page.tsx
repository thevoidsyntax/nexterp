'use client';

import { useEffect, useState, useCallback } from 'react';
import {
  Shield, Check, X, Loader2, ShoppingCart, Package, Truck, Users,
  Calculator, Briefcase, BadgeCheck, Boxes, BarChart3, type LucideIcon,
} from 'lucide-react';
import { organizationModulesApi, type OrganizationModuleDto } from '@/lib/api';
import { useAuthStore } from '@/lib/store';
import { PageHeader } from '@/components/PageHeader';
import { SkeletonLoader } from '@/components/SkeletonLoader';
import { useToast } from '@/hooks/useToast';

const MODULE_ICONS: Record<string, LucideIcon> = {
  sales: ShoppingCart,
  inventory: Package,
  purchasing: Truck,
  hrm: Users,
  accounting: Calculator,
  projects: Briefcase,
  quality: BadgeCheck,
  assets: Boxes,
  analytics: BarChart3,
};

function getModuleIcon(moduleCode: string): LucideIcon {
  return MODULE_ICONS[moduleCode.toLowerCase()] || Shield;
}

export default function ModulesPage() {
  const { user } = useAuthStore();
  const organizationId = user?.organizationId;
  const [modules, setModules] = useState<OrganizationModuleDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [togglingCode, setTogglingCode] = useState<string | null>(null);
  const toast = useToast();

  const fetchModules = useCallback(async () => {
    if (!organizationId) return;
    setLoading(true);
    try {
      const result = await organizationModulesApi.getAll(organizationId);
      if (result?.success && result.data) {
        setModules(result.data);
      }
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to load modules';
      toast('error', 'Error', msg);
    } finally {
      setLoading(false);
    }
  }, [organizationId, toast]);

  useEffect(() => { queueMicrotask(() => fetchModules()); }, [fetchModules]);

  const toggle = async (mod: OrganizationModuleDto) => {
    if (!organizationId) return;
    setTogglingCode(mod.moduleCode);
    try {
      if (mod.isEnabled) {
        await organizationModulesApi.disable(organizationId, mod.moduleCode);
        toast('success', 'Disabled', `${mod.moduleName} has been disabled`);
      } else {
        await organizationModulesApi.enable(organizationId, mod.moduleCode);
        toast('success', 'Enabled', `${mod.moduleName} has been enabled`);
      }
      await fetchModules();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to update module';
      toast('error', 'Error', msg);
    } finally {
      setTogglingCode(null);
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title="Modules"
        subtitle={`${modules.filter(m => m.isEnabled).length} of ${modules.length} modules enabled`}
        breadcrumbs={[
          { label: 'Dashboard', href: '/dashboard' },
          { label: 'Modules' },
        ]}
      />

      <div className="bg-surface rounded-xl border border-border-subtle overflow-hidden">
        {loading ? (
          <div className="p-6">
            <SkeletonLoader rows={5} height="h-12" />
          </div>
        ) : (
          <table className="w-full">
            <thead className="bg-surface-muted">
              <tr>
                <th className="px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase">Module</th>
                <th className="px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase">Code</th>
                <th className="px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase">Tier</th>
                <th className="px-4 py-3 text-center text-xs font-semibold text-slate-500 uppercase">Status</th>
                <th className="px-4 py-3 text-center text-xs font-semibold text-slate-500 uppercase">Toggle</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border-subtle">
              {modules.map((mod) => {
                const ModuleIcon = getModuleIcon(mod.moduleCode);
                return (
                <tr key={mod.moduleCode} className="hover:bg-surface-muted transition-colors">
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 rounded-lg bg-surface-muted flex items-center justify-center">
                        <ModuleIcon className="w-5 h-5 text-slate-500" />
                      </div>
                      <div>
                        <p className="font-medium text-foreground">{mod.moduleName}</p>
                        {mod.description && <p className="text-xs text-slate-500">{mod.description}</p>}
                      </div>
                    </div>
                  </td>
                  <td className="px-4 py-3">
                    <span className="px-2 py-0.5 rounded text-xs font-medium bg-surface-muted text-slate-500">{mod.moduleCode}</span>
                  </td>
                  <td className="px-4 py-3">
                    <span className="text-sm text-slate-500 capitalize">{mod.tier?.toLowerCase() || '-'}</span>
                  </td>
                  <td className="px-4 py-3 text-center">
                    <span className={`px-2 py-1 text-xs rounded-full font-medium ${mod.isEnabled ? 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400' : 'bg-slate-100 text-slate-500 dark:bg-slate-700'}`}>
                      {mod.isEnabled ? 'Enabled' : 'Disabled'}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-center">
                    <button
                      onClick={() => toggle(mod)}
                      disabled={togglingCode === mod.moduleCode}
                      aria-label={mod.isEnabled ? `Disable ${mod.moduleName}` : `Enable ${mod.moduleName}`}
                      className={`p-2 rounded-lg disabled:opacity-50 ${mod.isEnabled ? 'text-green-600 hover:bg-green-50' : 'text-slate-400 hover:bg-surface-muted'}`}
                    >
                      {togglingCode === mod.moduleCode ? (
                        <Loader2 className="w-4 h-4 animate-spin" />
                      ) : mod.isEnabled ? (
                        <X className="w-4 h-4" />
                      ) : (
                        <Check className="w-4 h-4" />
                      )}
                    </button>
                  </td>
                </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
