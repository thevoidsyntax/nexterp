'use client';

import { Plus, Building2, MapPin, Phone, Mail, Edit2, Trash2 } from 'lucide-react';
import { PageHeader } from '@/components/PageHeader';

// TODO: No backend CRUD exists yet for organizations (only per-organization
// module enable/disable, see OrganizationModulesController) - this page is
// demo data until that API is built. Add/Edit/Delete are disabled rather
// than silently doing nothing.
const ORGS = [
  { id: '1', name: 'Demo Corporation', code: 'DEMO', taxId: '01.234.567.8-901.000', email: 'admin@demo.com', phone: '+62 21 1234 5678', city: 'Jakarta Selatan', country: 'Indonesia', active: true, users: 8, modules: 5 },
];

export default function OrganizationsPage() {
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
          <button disabled title="Not available yet - backend API pending" className="flex items-center gap-2 px-3 py-2 bg-primary text-primary-foreground rounded-lg text-sm opacity-50 cursor-not-allowed">
            <Plus className="w-4 h-4" /> Add Organization
          </button>
        }
      />

      <div className="grid gap-4">
        {ORGS.map(org => (
          <div key={org.id} className="bg-surface rounded-xl border border-border-subtle p-4">
            <div className="flex items-start justify-between">
              <div className="flex items-start gap-4">
                <div className="w-12 h-12 rounded-xl bg-surface-muted flex items-center justify-center">
                  <Building2 className="w-6 h-6 text-slate-500" />
                </div>
                <div>
                  <div className="flex items-center gap-2">
                    <h3 className="font-heading font-semibold text-foreground">{org.name}</h3>
                    <span className={`px-2 py-0.5 text-xs rounded-full ${org.active ? 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400' : 'bg-surface-muted text-slate-500'}`}>
                      {org.active ? 'Active' : 'Inactive'}
                    </span>
                  </div>
                  <p className="text-sm text-slate-500">{org.code} | NPWP: {org.taxId}</p>
                  <div className="flex items-center gap-4 mt-2 text-sm text-slate-500">
                    <span className="flex items-center gap-1"><Mail className="w-3.5 h-3.5" />{org.email}</span>
                    <span className="flex items-center gap-1"><Phone className="w-3.5 h-3.5" />{org.phone}</span>
                    <span className="flex items-center gap-1"><MapPin className="w-3.5 h-3.5" />{org.city}, {org.country}</span>
                  </div>
                </div>
              </div>
              <div className="flex items-center gap-4">
                <div className="text-right">
                  <p className="font-heading text-lg font-bold text-foreground">{org.users}</p>
                  <p className="text-xs text-slate-500">Users</p>
                </div>
                <div className="text-right mr-2">
                  <p className="font-heading text-lg font-bold text-foreground">{org.modules}</p>
                  <p className="text-xs text-slate-500">Modules</p>
                </div>
                <button disabled aria-label="Edit organization" title="Not available yet - backend API pending" className="p-2 text-slate-300 dark:text-slate-600 rounded-lg cursor-not-allowed">
                  <Edit2 className="w-4 h-4" />
                </button>
                <button disabled aria-label="Delete organization" title="Not available yet - backend API pending" className="p-2 text-slate-300 dark:text-slate-600 rounded-lg cursor-not-allowed">
                  <Trash2 className="w-4 h-4" />
                </button>
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
