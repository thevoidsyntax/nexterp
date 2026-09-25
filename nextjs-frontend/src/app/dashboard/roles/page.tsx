'use client';

import { useEffect, useState, useCallback } from 'react';
import { Plus, Shield, Edit2, Trash2, X, Loader2 } from 'lucide-react';
import { rolesApi, type RoleDto } from '@/lib/api';
import { PageHeader } from '@/components/PageHeader';
import { SkeletonLoader } from '@/components/SkeletonLoader';
import { ConfirmDialog } from '@/components/ConfirmDialog';
import { useToast } from '@/hooks/useToast';
import { useAuthStore } from '@/lib/store';

type RoleFormData = {
  name: string;
  description: string;
  isActive: boolean;
  permissions: string[];
};

const EMPTY_FORM: RoleFormData = { name: '', description: '', isActive: true, permissions: [] };

export default function RolesPage() {
  const { user } = useAuthStore();
  const organizationId = user?.organizationId;
  const [roles, setRoles] = useState<RoleDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [availablePermissions, setAvailablePermissions] = useState<string[]>([]);
  const [showModal, setShowModal] = useState(false);
  const [editingRole, setEditingRole] = useState<RoleDto | null>(null);
  const [formData, setFormData] = useState<RoleFormData>(EMPTY_FORM);
  const [saving, setSaving] = useState(false);
  const [deleteConfirm, setDeleteConfirm] = useState<{ isOpen: boolean; roleId: string | null; roleName: string }>({
    isOpen: false,
    roleId: null,
    roleName: '',
  });

  const toast = useToast();

  const fetchRoles = useCallback(async () => {
    setLoading(true);
    try {
      const result = await rolesApi.getAll({ organizationId, pageSize: 100 });
      if (result?.success && result.data) {
        setRoles(result.data.items || []);
      }
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to load roles';
      toast('error', 'Error', msg);
    } finally {
      setLoading(false);
    }
  }, [organizationId, toast]);

  const fetchPermissions = useCallback(async () => {
    try {
      const result = await rolesApi.getPermissions();
      if (result?.success && result.data) {
        setAvailablePermissions(result.data.permissions || []);
      }
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to load permissions';
      toast('error', 'Error', msg);
    }
  }, [toast]);

  useEffect(() => { queueMicrotask(() => fetchRoles()); }, [fetchRoles]);
  useEffect(() => { queueMicrotask(() => fetchPermissions()); }, [fetchPermissions]);

  const openCreate = () => {
    setEditingRole(null);
    setFormData(EMPTY_FORM);
    setShowModal(true);
  };

  const openEdit = (role: RoleDto) => {
    setEditingRole(role);
    setFormData({
      name: role.name,
      description: role.description || '',
      isActive: role.isActive,
      permissions: role.permissions,
    });
    setShowModal(true);
  };

  const togglePermission = (perm: string) => {
    setFormData((prev) => ({
      ...prev,
      permissions: prev.permissions.includes(perm)
        ? prev.permissions.filter((p) => p !== perm)
        : [...prev.permissions, perm],
    }));
  };

  const handleSave = async () => {
    setSaving(true);
    try {
      if (editingRole) {
        await rolesApi.update(editingRole.id, {
          name: formData.name,
          description: formData.description || undefined,
          isActive: formData.isActive,
        });
        toast('success', 'Updated!', 'Role has been updated');
      } else {
        await rolesApi.create({
          name: formData.name,
          description: formData.description || undefined,
          permissions: formData.permissions,
        });
        toast('success', 'Created!', 'Role has been added');
      }
      setShowModal(false);
      fetchRoles();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to save role';
      toast('error', 'Error', msg);
    } finally {
      setSaving(false);
    }
  };

  const confirmDelete = (role: RoleDto) => {
    setDeleteConfirm({ isOpen: true, roleId: role.id, roleName: role.name });
  };

  const handleDelete = async () => {
    if (!deleteConfirm.roleId) return;
    try {
      await rolesApi.delete(deleteConfirm.roleId);
      toast('success', 'Deleted!', 'Role has been removed');
      setDeleteConfirm({ isOpen: false, roleId: null, roleName: '' });
      fetchRoles();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to delete role';
      toast('error', 'Error', msg);
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title="Roles & Permissions"
        subtitle="Manage user roles and access control"
        breadcrumbs={[
          { label: 'Dashboard', href: '/dashboard' },
          { label: 'Roles' },
        ]}
        actions={
          <button onClick={openCreate} className="flex items-center gap-2 px-4 py-2 bg-primary hover:bg-primary/90 text-primary-foreground rounded-lg transition">
            <Plus className="w-4 h-4" /> Add Role
          </button>
        }
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
                <th className="px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase">Role</th>
                <th className="px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase">Permissions</th>
                <th className="px-4 py-3 text-center text-xs font-semibold text-slate-500 uppercase">Users</th>
                <th className="px-4 py-3 text-center text-xs font-semibold text-slate-500 uppercase">Type</th>
                <th className="px-4 py-3 text-right text-xs font-semibold text-slate-500 uppercase">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border-subtle">
              {roles.map(role => (
                <tr key={role.id} className="hover:bg-surface-muted transition-colors">
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-3">
                      <div className="w-8 h-8 rounded-lg bg-surface-muted flex items-center justify-center">
                        <Shield className="w-4 h-4 text-slate-500" />
                      </div>
                      <div>
                        <p className="font-medium text-foreground">{role.name}</p>
                        {role.description && <p className="text-xs text-slate-500">{role.description}</p>}
                      </div>
                    </div>
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex flex-wrap gap-1">
                      {role.permissions.slice(0, 3).map(p => (
                        <span key={p} className="px-2 py-0.5 bg-surface-muted text-xs rounded text-slate-600 dark:text-slate-300">{p}</span>
                      ))}
                      {role.permissions.length > 3 && (
                        <span className="px-2 py-0.5 text-xs text-slate-400">+{role.permissions.length - 3}</span>
                      )}
                    </div>
                  </td>
                  <td className="px-4 py-3 text-center">
                    <span className="text-sm text-slate-600 dark:text-slate-300">{role.userCount}</span>
                  </td>
                  <td className="px-4 py-3 text-center">
                    <span className={`px-2 py-1 text-xs rounded-full font-medium ${role.isSystemRole ? 'bg-purple-100 text-purple-700 dark:bg-purple-900/30 dark:text-purple-400' : 'bg-surface-muted text-slate-600 dark:text-slate-300'}`}>
                      {role.isSystemRole ? 'System' : 'Custom'}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-right">
                    {role.isSystemRole ? (
                      <span title="System roles cannot be modified" className="p-1.5 inline-block text-slate-300 dark:text-slate-600"><Edit2 className="w-4 h-4" /></span>
                    ) : (
                      <>
                        <button onClick={() => openEdit(role)} aria-label="Edit role" className="p-1.5 text-slate-400 hover:text-primary hover:bg-primary/10 rounded"><Edit2 className="w-4 h-4" /></button>
                        <button onClick={() => confirmDelete(role)} aria-label="Delete role" className="p-1.5 text-slate-400 hover:text-red-600 hover:bg-red-50 rounded ml-1"><Trash2 className="w-4 h-4" /></button>
                      </>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* Create/Edit Modal */}
      {showModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50" onClick={(e) => e.target === e.currentTarget && setShowModal(false)}>
          <div className="bg-surface rounded-xl shadow-xl w-full max-w-lg mx-4 max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between p-5 border-b border-border-subtle">
              <h3 className="text-lg font-heading font-semibold text-foreground">{editingRole ? 'Edit Role' : 'Add Role'}</h3>
              <button onClick={() => setShowModal(false)} aria-label="Close dialog" className="p-1 hover:bg-surface-muted rounded transition-colors"><X className="w-5 h-5" /></button>
            </div>
            <div className="p-5 space-y-4">
              <div>
                <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Name *</label>
                <input type="text" value={formData.name} onChange={(e) => setFormData({ ...formData, name: e.target.value })} className="w-full px-3 py-2 border border-border-subtle rounded-lg bg-surface text-foreground focus:ring-2 focus:ring-primary focus:border-primary" required disabled={editingRole?.isSystemRole} />
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Description</label>
                <input type="text" value={formData.description} onChange={(e) => setFormData({ ...formData, description: e.target.value })} className="w-full px-3 py-2 border border-border-subtle rounded-lg bg-surface text-foreground focus:ring-2 focus:ring-primary focus:border-primary" />
              </div>
              {editingRole && (
                <label className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-300">
                  <input type="checkbox" checked={formData.isActive} onChange={(e) => setFormData({ ...formData, isActive: e.target.checked })} className="rounded border-border-subtle text-primary focus:ring-primary" />
                  Active
                </label>
              )}
              {!editingRole && (
                <div>
                  <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-2">Permissions</label>
                  <div className="max-h-48 overflow-y-auto border border-border-subtle rounded-lg p-3 space-y-1">
                    {availablePermissions.map((perm) => (
                      <label key={perm} className="flex items-center gap-2 text-sm text-slate-600 dark:text-slate-300">
                        <input type="checkbox" checked={formData.permissions.includes(perm)} onChange={() => togglePermission(perm)} className="rounded border-border-subtle text-primary focus:ring-primary" />
                        {perm}
                      </label>
                    ))}
                  </div>
                </div>
              )}
            </div>
            <div className="p-5 border-t border-border-subtle flex gap-3 justify-end">
              <button onClick={() => setShowModal(false)} className="px-4 py-2 border border-border-subtle rounded-lg hover:bg-surface-muted transition-colors">Cancel</button>
              <button onClick={handleSave} disabled={saving || !formData.name} className="px-4 py-2 bg-primary hover:bg-primary/90 text-primary-foreground rounded-lg disabled:opacity-50 flex items-center gap-2 transition-colors">
                {saving && <Loader2 className="w-4 h-4 animate-spin" />}
                {editingRole ? 'Update' : 'Create'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Delete Confirmation Dialog */}
      <ConfirmDialog
        isOpen={deleteConfirm.isOpen}
        title="Delete Role?"
        message={`Are you sure you want to delete ${deleteConfirm.roleName}? This action cannot be undone.`}
        confirmText="Delete"
        cancelText="Cancel"
        onConfirm={handleDelete}
        onCancel={() => setDeleteConfirm({ isOpen: false, roleId: null, roleName: '' })}
        variant="danger"
      />
    </div>
  );
}
