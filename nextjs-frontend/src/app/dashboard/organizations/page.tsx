'use client';

import { useEffect, useState, useCallback } from 'react';
import { Plus, Building2, MapPin, Phone, Mail, Edit2, Trash2, X, Loader2 } from 'lucide-react';
import { organizationsApi, type OrganizationDto, type OrganizationFormData } from '@/lib/api';
import { PageHeader } from '@/components/PageHeader';
import { SkeletonLoader } from '@/components/SkeletonLoader';
import { ConfirmDialog } from '@/components/ConfirmDialog';
import { useToast } from '@/hooks/useToast';
import { useAuthStore } from '@/lib/store';

const EMPTY_FORM: OrganizationFormData = {
  name: '', code: '', taxId: '', phone: '', email: '', address: '', city: '', country: '', postalCode: '',
};

export default function OrganizationsPage() {
  const { user } = useAuthStore();
  const isSuperAdmin = user?.isSuperAdmin ?? false;
  const [orgs, setOrgs] = useState<OrganizationDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [editingOrg, setEditingOrg] = useState<OrganizationDto | null>(null);
  const [formData, setFormData] = useState<OrganizationFormData>(EMPTY_FORM);
  const [formIsActive, setFormIsActive] = useState(true);
  const [saving, setSaving] = useState(false);
  const [deleteConfirm, setDeleteConfirm] = useState<{ isOpen: boolean; orgId: string | null; orgName: string }>({
    isOpen: false,
    orgId: null,
    orgName: '',
  });

  const toast = useToast();

  const fetchOrgs = useCallback(async () => {
    setLoading(true);
    try {
      const result = await organizationsApi.getAll({ pageSize: 100 });
      if (result?.success && result.data) {
        setOrgs(result.data.items || []);
      }
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to load organizations';
      toast('error', 'Error', msg);
    } finally {
      setLoading(false);
    }
  }, [toast]);

  useEffect(() => { queueMicrotask(() => fetchOrgs()); }, [fetchOrgs]);

  const openCreate = () => {
    setEditingOrg(null);
    setFormData(EMPTY_FORM);
    setFormIsActive(true);
    setShowModal(true);
  };

  const openEdit = (org: OrganizationDto) => {
    setEditingOrg(org);
    setFormData({
      name: org.name,
      code: org.code || '',
      taxId: org.taxId || '',
      phone: org.phone || '',
      email: org.email || '',
      address: org.address || '',
      city: org.city || '',
      country: org.country || '',
      postalCode: org.postalCode || '',
    });
    setFormIsActive(org.isActive);
    setShowModal(true);
  };

  const handleSave = async () => {
    setSaving(true);
    try {
      if (editingOrg) {
        await organizationsApi.update(editingOrg.id, { ...formData, isActive: formIsActive });
        toast('success', 'Updated!', 'Organization has been updated');
      } else {
        await organizationsApi.create(formData);
        toast('success', 'Created!', 'Organization has been added');
      }
      setShowModal(false);
      fetchOrgs();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to save organization';
      toast('error', 'Error', msg);
    } finally {
      setSaving(false);
    }
  };

  const confirmDelete = (org: OrganizationDto) => {
    setDeleteConfirm({ isOpen: true, orgId: org.id, orgName: org.name });
  };

  const handleDelete = async () => {
    if (!deleteConfirm.orgId) return;
    try {
      await organizationsApi.delete(deleteConfirm.orgId);
      toast('success', 'Deleted!', 'Organization has been removed');
      setDeleteConfirm({ isOpen: false, orgId: null, orgName: '' });
      fetchOrgs();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to delete organization';
      toast('error', 'Error', msg);
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title="Organizations"
        subtitle="Manage organizations and tenants"
        breadcrumbs={[
          { label: 'Dashboard', href: '/dashboard' },
          { label: 'Organizations' },
        ]}
        actions={
          isSuperAdmin ? (
            <button onClick={openCreate} className="flex items-center gap-2 px-3 py-2 bg-primary hover:bg-primary/90 text-primary-foreground rounded-lg text-sm transition">
              <Plus className="w-4 h-4" /> Add Organization
            </button>
          ) : (
            <button disabled title="Only a super administrator can create organizations" className="flex items-center gap-2 px-3 py-2 bg-primary text-primary-foreground rounded-lg text-sm opacity-50 cursor-not-allowed">
              <Plus className="w-4 h-4" /> Add Organization
            </button>
          )
        }
      />

      {loading ? (
        <div className="bg-surface rounded-xl border border-border-subtle p-6">
          <SkeletonLoader rows={3} height="h-20" />
        </div>
      ) : (
        <div className="grid gap-4">
          {orgs.map(org => (
            <div key={org.id} className="bg-surface rounded-xl border border-border-subtle p-4">
              <div className="flex items-start justify-between">
                <div className="flex items-start gap-4">
                  <div className="w-12 h-12 rounded-xl bg-surface-muted flex items-center justify-center">
                    <Building2 className="w-6 h-6 text-slate-500" />
                  </div>
                  <div>
                    <div className="flex items-center gap-2">
                      <h3 className="font-heading font-semibold text-foreground">{org.name}</h3>
                      <span className={`px-2 py-0.5 text-xs rounded-full ${org.isActive ? 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400' : 'bg-surface-muted text-slate-500'}`}>
                        {org.isActive ? 'Active' : 'Inactive'}
                      </span>
                    </div>
                    <p className="text-sm text-slate-500">{org.code}{org.taxId ? ` | NPWP: ${org.taxId}` : ''}</p>
                    <div className="flex items-center gap-4 mt-2 text-sm text-slate-500">
                      {org.email && <span className="flex items-center gap-1"><Mail className="w-3.5 h-3.5" />{org.email}</span>}
                      {org.phone && <span className="flex items-center gap-1"><Phone className="w-3.5 h-3.5" />{org.phone}</span>}
                      {(org.city || org.country) && <span className="flex items-center gap-1"><MapPin className="w-3.5 h-3.5" />{[org.city, org.country].filter(Boolean).join(', ')}</span>}
                    </div>
                  </div>
                </div>
                <div className="flex items-center gap-4">
                  <div className="text-right">
                    <p className="font-heading text-lg font-bold text-foreground">{org.userCount}</p>
                    <p className="text-xs text-slate-500">Users</p>
                  </div>
                  <div className="text-right mr-2">
                    <p className="font-heading text-lg font-bold text-foreground">{org.moduleCount}</p>
                    <p className="text-xs text-slate-500">Modules</p>
                  </div>
                  <button onClick={() => openEdit(org)} aria-label="Edit organization" className="p-2 text-slate-400 hover:text-primary hover:bg-primary/10 rounded-lg transition-colors">
                    <Edit2 className="w-4 h-4" />
                  </button>
                  {isSuperAdmin ? (
                    <button onClick={() => confirmDelete(org)} aria-label="Delete organization" className="p-2 text-slate-400 hover:text-red-600 hover:bg-red-50 rounded-lg transition-colors">
                      <Trash2 className="w-4 h-4" />
                    </button>
                  ) : (
                    <span title="Only a super administrator can delete organizations" className="p-2 inline-block text-slate-300 dark:text-slate-600">
                      <Trash2 className="w-4 h-4" />
                    </span>
                  )}
                </div>
              </div>
            </div>
          ))}
          {orgs.length === 0 && (
            <div className="bg-surface rounded-xl border border-border-subtle p-8 text-center text-slate-500">
              No organizations found.
            </div>
          )}
        </div>
      )}

      {/* Create/Edit Modal */}
      {showModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50" onClick={(e) => e.target === e.currentTarget && setShowModal(false)}>
          <div className="bg-surface rounded-xl shadow-xl w-full max-w-lg mx-4 max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between p-5 border-b border-border-subtle">
              <h3 className="text-lg font-heading font-semibold text-foreground">{editingOrg ? 'Edit Organization' : 'Add Organization'}</h3>
              <button onClick={() => setShowModal(false)} aria-label="Close dialog" className="p-1 hover:bg-surface-muted rounded transition-colors"><X className="w-5 h-5" /></button>
            </div>
            <div className="p-5 space-y-4">
              <div>
                <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Name *</label>
                <input type="text" value={formData.name} onChange={(e) => setFormData({ ...formData, name: e.target.value })} className="w-full px-3 py-2 border border-border-subtle rounded-lg bg-surface text-foreground focus:ring-2 focus:ring-primary focus:border-primary" required />
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Code</label>
                  <input type="text" value={formData.code} onChange={(e) => setFormData({ ...formData, code: e.target.value })} className="w-full px-3 py-2 border border-border-subtle rounded-lg bg-surface text-foreground focus:ring-2 focus:ring-primary focus:border-primary" />
                </div>
                <div>
                  <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">NPWP</label>
                  <input type="text" value={formData.taxId} onChange={(e) => setFormData({ ...formData, taxId: e.target.value })} className="w-full px-3 py-2 border border-border-subtle rounded-lg bg-surface text-foreground focus:ring-2 focus:ring-primary focus:border-primary" />
                </div>
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Email</label>
                  <input type="email" value={formData.email} onChange={(e) => setFormData({ ...formData, email: e.target.value })} className="w-full px-3 py-2 border border-border-subtle rounded-lg bg-surface text-foreground focus:ring-2 focus:ring-primary focus:border-primary" />
                </div>
                <div>
                  <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Phone</label>
                  <input type="text" value={formData.phone} onChange={(e) => setFormData({ ...formData, phone: e.target.value })} className="w-full px-3 py-2 border border-border-subtle rounded-lg bg-surface text-foreground focus:ring-2 focus:ring-primary focus:border-primary" />
                </div>
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Address</label>
                <input type="text" value={formData.address} onChange={(e) => setFormData({ ...formData, address: e.target.value })} className="w-full px-3 py-2 border border-border-subtle rounded-lg bg-surface text-foreground focus:ring-2 focus:ring-primary focus:border-primary" />
              </div>
              <div className="grid grid-cols-3 gap-3">
                <div>
                  <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">City</label>
                  <input type="text" value={formData.city} onChange={(e) => setFormData({ ...formData, city: e.target.value })} className="w-full px-3 py-2 border border-border-subtle rounded-lg bg-surface text-foreground focus:ring-2 focus:ring-primary focus:border-primary" />
                </div>
                <div>
                  <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Country</label>
                  <input type="text" value={formData.country} onChange={(e) => setFormData({ ...formData, country: e.target.value })} className="w-full px-3 py-2 border border-border-subtle rounded-lg bg-surface text-foreground focus:ring-2 focus:ring-primary focus:border-primary" />
                </div>
                <div>
                  <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Postal Code</label>
                  <input type="text" value={formData.postalCode} onChange={(e) => setFormData({ ...formData, postalCode: e.target.value })} className="w-full px-3 py-2 border border-border-subtle rounded-lg bg-surface text-foreground focus:ring-2 focus:ring-primary focus:border-primary" />
                </div>
              </div>
              {editingOrg && (
                <label className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-300">
                  <input type="checkbox" checked={formIsActive} onChange={(e) => setFormIsActive(e.target.checked)} className="rounded border-border-subtle text-primary focus:ring-primary" />
                  Active
                </label>
              )}
            </div>
            <div className="p-5 border-t border-border-subtle flex gap-3 justify-end">
              <button onClick={() => setShowModal(false)} className="px-4 py-2 border border-border-subtle rounded-lg hover:bg-surface-muted transition-colors">Cancel</button>
              <button onClick={handleSave} disabled={saving || !formData.name} className="px-4 py-2 bg-primary hover:bg-primary/90 text-primary-foreground rounded-lg disabled:opacity-50 flex items-center gap-2 transition-colors">
                {saving && <Loader2 className="w-4 h-4 animate-spin" />}
                {editingOrg ? 'Update' : 'Create'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Delete Confirmation Dialog */}
      <ConfirmDialog
        isOpen={deleteConfirm.isOpen}
        title="Delete Organization?"
        message={`Are you sure you want to delete ${deleteConfirm.orgName}? This action cannot be undone.`}
        confirmText="Delete"
        cancelText="Cancel"
        onConfirm={handleDelete}
        onCancel={() => setDeleteConfirm({ isOpen: false, orgId: null, orgName: '' })}
        variant="danger"
      />
    </div>
  );
}
