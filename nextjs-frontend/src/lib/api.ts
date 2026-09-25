import axios from 'axios';
import { useAuthStore } from './store';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'https://api-production-ab1b.up.railway.app';

// Auth is carried by the HttpOnly cookies the API sets on login (see
// AuthController), not by an Authorization header built from a token this app
// can read — withCredentials sends those cookies on every cross-origin request.
const api = axios.create({
  baseURL: `${API_BASE_URL}/api/v1`,
  withCredentials: true,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Handle auth errors
api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      if (typeof window !== 'undefined') {
        useAuthStore.getState().logout();
        window.location.href = '/login';
      }
    }
    return Promise.reject(error);
  }
);

// ─── Common Types ────────────────────────────────────────────

export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  success: boolean;
  data?: {
    accessToken: string;
    refreshToken: string;
    expiresAt: string;
    user: UserData;
  };
  error?: string;
}

export interface UserData {
  id: string;
  username: string;
  email: string;
  firstName: string;
  lastName: string;
  fullName: string;
  organizationId: string;
  isActive: boolean;
  isSuperAdmin: boolean;
  roles: string[];
}

export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  error?: string;
}

export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// ─── Auth API ────────────────────────────────────────────────

export const authApi = {
  login: async (data: LoginRequest): Promise<LoginResponse> => {
    const response = await api.post<LoginResponse>('/auth/login', data);
    return response.data;
  },
  // Clears the HttpOnly auth cookies server-side and the local UI state — the one
  // place both steps happen together, so every "Logout" action behaves the same way.
  logout: async (): Promise<void> => {
    // Best-effort: still clear local state below even if this request fails
    // (e.g. the cookies already expired) — never block logging out client-side.
    try { await api.post('/auth/logout'); } catch { /* ignore */ }
    if (typeof window !== 'undefined') {
      localStorage.removeItem('nexterp_token');
      localStorage.removeItem('nexterp_user');
    }
    useAuthStore.getState().logout();
  },
  refresh: async (refreshToken: string): Promise<LoginResponse> => {
    const response = await api.post<LoginResponse>('/auth/refresh', { refreshToken });
    return response.data;
  },
};

// ─── Modules API ───────────────────────────────────────────

export const modulesApi = {
  getAll: async () => {
    const response = await api.get<ApiResponse<ModuleDto[]>>('/modules');
    return response.data;
  },
  getEnabled: async (orgId: string) => {
    const response = await api.get<ApiResponse<ModuleDto[]>>(`/modules/enabled/${orgId}`);
    return response.data;
  },
};

interface ModuleDto {
  id: string;
  name: string;
  code: string;
  description?: string;
  isEnabled: boolean;
}

// Per-organization module enable/disable - a different backend controller
// (OrganizationModulesController) than modulesApi above, which only reads
// the static module catalog.
export const organizationModulesApi = {
  getAll: async (organizationId: string) => {
    const response = await api.get<ApiResponse<OrganizationModuleDto[]>>(`/organizations/${organizationId}/modules`);
    return response.data;
  },
  enable: async (organizationId: string, moduleCode: string) => {
    const response = await api.post<ApiResponse<void>>(`/organizations/${organizationId}/modules/${moduleCode}/enable`);
    return response.data;
  },
  disable: async (organizationId: string, moduleCode: string) => {
    const response = await api.delete<ApiResponse<void>>(`/organizations/${organizationId}/modules/${moduleCode}/disable`);
    return response.data;
  },
};

export interface OrganizationModuleDto {
  id: string;
  moduleCode: string;
  moduleName: string;
  description?: string;
  isEnabled: boolean;
  tier: string;
  isPremium: boolean;
}

// ─── Roles API ───────────────────────────────────────────────

export const rolesApi = {
  getAll: async (params?: { organizationId?: string; page?: number; pageSize?: number; search?: string; isActive?: boolean }) => {
    const response = await api.get<ApiResponse<PaginatedResponse<RoleDto>>>('/roles', { params });
    return response.data;
  },
  create: async (data: { name: string; description?: string; permissions?: string[] }) => {
    const response = await api.post<ApiResponse<string>>('/roles', data);
    return response.data;
  },
  update: async (id: string, data: { name: string; description?: string; isActive: boolean }) => {
    const response = await api.put<ApiResponse<void>>(`/roles/${id}`, data);
    return response.data;
  },
  delete: async (id: string) => {
    const response = await api.delete<ApiResponse<void>>(`/roles/${id}`);
    return response.data;
  },
  getPermissions: async () => {
    const response = await api.get<ApiResponse<{ permissions: string[] }>>('/roles/permissions');
    return response.data;
  },
};

export interface RoleDto {
  id: string;
  organizationId: string;
  name: string;
  description?: string;
  isActive: boolean;
  isSystemRole: boolean;
  userCount: number;
  permissionCount: number;
  permissions: string[];
  createdAt: string;
  updatedAt?: string;
}

// ─── Users API (self-service: profile + password) ────────────

export const usersApi = {
  update: async (id: string, data: { firstName: string; lastName: string; phone?: string; isActive: boolean }) => {
    const response = await api.put<ApiResponse<void>>(`/users/${id}`, data);
    return response.data;
  },
  changePassword: async (id: string, data: { currentPassword: string; newPassword: string }) => {
    const response = await api.post<ApiResponse<void>>(`/users/${id}/change-password`, data);
    return response.data;
  },
};

// ─── Dashboard API ────────────────────────────────────────────

export const dashboardApi = {
  getStats: async () => {
    const response = await api.get<ApiResponse<DashboardStats>>('/dashboard/stats');
    return response.data;
  },
};

// ─── HRM APIs ────────────────────────────────────────────────

export const employeesApi = {
  getAll: async (params?: { page?: number; pageSize?: number; search?: string }) => {
    const response = await api.get<ApiResponse<PaginatedResponse<EmployeeDto>>>('/employees', { params });
    return response.data;
  },
  getById: async (id: string) => {
    const response = await api.get<ApiResponse<EmployeeDto>>(`/employees/${id}`);
    return response.data;
  },
  create: async (data: Partial<EmployeeDto>) => {
    const response = await api.post<ApiResponse<string>>('/employees', data);
    return response.data;
  },
  update: async (id: string, data: Partial<EmployeeDto>) => {
    const response = await api.put<ApiResponse<void>>(`/employees/${id}`, data);
    return response.data;
  },
  delete: async (id: string) => {
    const response = await api.delete<ApiResponse<void>>(`/employees/${id}`);
    return response.data;
  },
};

export const departmentsApi = {
  getAll: async (params?: { page?: number; pageSize?: number; search?: string }) => {
    const response = await api.get<ApiResponse<PaginatedResponse<DepartmentDto>>>('/departments', { params });
    return response.data;
  },
  create: async (data: Partial<DepartmentDto>) => {
    const response = await api.post<ApiResponse<string>>('/departments', data);
    return response.data;
  },
  update: async (id: string, data: Partial<DepartmentDto>) => {
    const response = await api.put<ApiResponse<void>>(`/departments/${id}`, data);
    return response.data;
  },
  delete: async (id: string) => {
    const response = await api.delete<ApiResponse<void>>(`/departments/${id}`);
    return response.data;
  },
};

// ─── Inventory APIs ───────────────────────────────────────────

export const stockItemsApi = {
  getAll: async (params?: { page?: number; pageSize?: number; search?: string }) => {
    const response = await api.get<ApiResponse<PaginatedResponse<StockItemDto>>>('/stock-items', { params });
    return response.data;
  },
  getById: async (id: string) => {
    const response = await api.get<ApiResponse<StockItemDto>>(`/stock-items/${id}`);
    return response.data;
  },
  create: async (data: Partial<StockItemDto>) => {
    const response = await api.post<ApiResponse<string>>('/stock-items', data);
    return response.data;
  },
  update: async (id: string, data: Partial<StockItemDto>) => {
    const response = await api.put<ApiResponse<void>>(`/stock-items/${id}`, data);
    return response.data;
  },
  delete: async (id: string) => {
    const response = await api.delete<ApiResponse<void>>(`/stock-items/${id}`);
    return response.data;
  },
};

export const warehousesApi = {
  getAll: async (params?: { page?: number; pageSize?: number; search?: string }) => {
    const response = await api.get<ApiResponse<PaginatedResponse<WarehouseDto>>>('/warehouses', { params });
    return response.data;
  },
  create: async (data: Partial<WarehouseDto>) => {
    const response = await api.post<ApiResponse<string>>('/warehouses', data);
    return response.data;
  },
  update: async (id: string, data: Partial<WarehouseDto>) => {
    const response = await api.put<ApiResponse<void>>(`/warehouses/${id}`, data);
    return response.data;
  },
  delete: async (id: string) => {
    const response = await api.delete<ApiResponse<void>>(`/warehouses/${id}`);
    return response.data;
  },
};

// ─── Purchasing APIs ───────────────────────────────────────────

export const suppliersApi = {
  getAll: async (params?: { page?: number; pageSize?: number; search?: string }) => {
    const response = await api.get<ApiResponse<PaginatedResponse<SupplierDto>>>('/suppliers', { params });
    return response.data;
  },
  create: async (data: Partial<SupplierDto>) => {
    const response = await api.post<ApiResponse<string>>('/suppliers', data);
    return response.data;
  },
  update: async (id: string, data: Partial<SupplierDto>) => {
    const response = await api.put<ApiResponse<void>>(`/suppliers/${id}`, data);
    return response.data;
  },
  delete: async (id: string) => {
    const response = await api.delete<ApiResponse<void>>(`/suppliers/${id}`);
    return response.data;
  },
};

export const purchaseOrdersApi = {
  getAll: async (params?: { page?: number; pageSize?: number; search?: string }) => {
    const response = await api.get<ApiResponse<PaginatedResponse<PurchaseOrderDto>>>('/purchase-orders', { params });
    return response.data;
  },
  getById: async (id: string) => {
    const response = await api.get<ApiResponse<PurchaseOrderDto>>(`/purchase-orders/${id}`);
    return response.data;
  },
  create: async (data: Partial<PurchaseOrderDto>) => {
    const response = await api.post<ApiResponse<string>>('/purchase-orders', data);
    return response.data;
  },
  submit: async (id: string) => {
    const response = await api.post<ApiResponse<void>>(`/purchase-orders/${id}/submit`);
    return response.data;
  },
  approve: async (id: string) => {
    const response = await api.post<ApiResponse<void>>(`/purchase-orders/${id}/approve`);
    return response.data;
  },
  cancel: async (id: string) => {
    const response = await api.post<ApiResponse<void>>(`/purchase-orders/${id}/cancel`);
    return response.data;
  },
};

// ─── Accounting APIs ──────────────────────────────────────────

export const accountsApi = {
  getAll: async (params?: { page?: number; pageSize?: number; search?: string }) => {
    const response = await api.get<ApiResponse<PaginatedResponse<AccountDto>>>('/accounts', { params });
    return response.data;
  },
  create: async (data: Partial<AccountDto>) => {
    const response = await api.post<ApiResponse<string>>('/accounts', data);
    return response.data;
  },
  update: async (id: string, data: Partial<AccountDto>) => {
    const response = await api.put<ApiResponse<void>>(`/accounts/${id}`, data);
    return response.data;
  },
  delete: async (id: string) => {
    const response = await api.delete<ApiResponse<void>>(`/accounts/${id}`);
    return response.data;
  },
};

export const journalEntriesApi = {
  getAll: async (params?: { page?: number; pageSize?: number; search?: string }) => {
    const response = await api.get<ApiResponse<PaginatedResponse<JournalEntryDto>>>('/journal-entries', { params });
    return response.data;
  },
  create: async (data: Partial<JournalEntryDto>) => {
    const response = await api.post<ApiResponse<string>>('/journal-entries', data);
    return response.data;
  },
};

// ─── Projects APIs ────────────────────────────────────────────

export const projectsApi = {
  getAll: async (params?: { page?: number; pageSize?: number; search?: string }) => {
    const response = await api.get<ApiResponse<PaginatedResponse<ProjectDto>>>('/projects', { params });
    return response.data;
  },
  getById: async (id: string) => {
    const response = await api.get<ApiResponse<ProjectDto>>(`/projects/${id}`);
    return response.data;
  },
  create: async (data: Partial<ProjectDto>) => {
    const response = await api.post<ApiResponse<string>>('/projects', data);
    return response.data;
  },
  update: async (id: string, data: Partial<ProjectDto>) => {
    const response = await api.put<ApiResponse<void>>(`/projects/${id}`, data);
    return response.data;
  },
  delete: async (id: string) => {
    const response = await api.delete<ApiResponse<void>>(`/projects/${id}`);
    return response.data;
  },
  start: async (id: string) => {
    const response = await api.post<ApiResponse<void>>(`/projects/${id}/start`);
    return response.data;
  },
  complete: async (id: string) => {
    const response = await api.post<ApiResponse<void>>(`/projects/${id}/complete`);
    return response.data;
  },
};

export const projectTasksApi = {
  getAll: async (params?: { page?: number; pageSize?: number; projectId?: string; search?: string }) => {
    const response = await api.get<ApiResponse<PaginatedResponse<ProjectTaskDto>>>('/project-tasks', { params });
    return response.data;
  },
  create: async (data: Partial<ProjectTaskDto>) => {
    const response = await api.post<ApiResponse<string>>('/project-tasks', data);
    return response.data;
  },
  updateStatus: async (id: string, status: string) => {
    const response = await api.post<ApiResponse<void>>(`/project-tasks/${id}/status`, { status });
    return response.data;
  },
  assign: async (id: string, assigneeId: string) => {
    const response = await api.post<ApiResponse<void>>(`/project-tasks/${id}/assign`, { assigneeId });
    return response.data;
  },
};

// ─── DTOs ────────────────────────────────────────────────────

export interface DashboardStats {
  totalEmployees?: number;
  totalInventoryItems?: number;
  totalPurchaseOrders?: number;
  totalSuppliers?: number;
  totalProjects?: number;
  totalAccounts?: number;
  recentActivities?: { description: string; module: string; timestamp: string }[];
}

export interface EmployeeDto {
  id: string;
  organizationId: string;
  employeeNumber?: string;
  firstName: string;
  lastName: string;
  email?: string;
  phone?: string;
  department?: string;
  position?: string;
  status?: string;
  hireDate?: string;
  isActive?: boolean;
}

export interface DepartmentDto {
  id: string;
  organizationId: string;
  name: string;
  code?: string;
  description?: string;
  parentDepartmentId?: string;
  isActive?: boolean;
}

export interface StockItemDto {
  id: string;
  organizationId: string;
  name: string;
  code: string;
  barcode?: string;
  category?: string;
  unitOfMeasure?: string;
  standardCost?: number;
  standardPrice?: number;
  reorderLevel?: number;
  isActive?: boolean;
}

export interface WarehouseDto {
  id: string;
  organizationId: string;
  name: string;
  code?: string;
  address?: string;
  city?: string;
  isActive?: boolean;
}

export interface SupplierDto {
  id: string;
  organizationId: string;
  supplierCode?: string;
  supplierName: string;
  type?: string;
  email?: string;
  phone?: string;
  isActive?: boolean;
}

export interface PurchaseOrderDto {
  id: string;
  organizationId: string;
  orderNumber?: string;
  supplierId?: string;
  supplierName?: string;
  orderDate?: string;
  expectedDeliveryDate?: string;
  status?: string;
  subtotal?: number;
  taxAmount?: number;
  totalAmount?: number;
}

export interface AccountDto {
  id: string;
  organizationId: string;
  accountCode: string;
  name: string;
  accountType?: string;
  class?: string;
  parentId?: string;
  isBankAccount?: boolean;
  isCashAccount?: boolean;
  openingBalance?: number;
  isActive?: boolean;
}

export interface JournalEntryDto {
  id: string;
  organizationId: string;
  entryNumber?: string;
  entryDate?: string;
  postingDate?: string;
  title?: string;
  status?: string;
  totalDebit?: number;
  totalCredit?: number;
  lines?: JournalLineDto[];
}

export interface JournalLineDto {
  accountId: string;
  accountName?: string;
  description?: string;
  debitAmount?: number;
  creditAmount?: number;
}

export interface ProjectDto {
  id: string;
  organizationId: string;
  name: string;
  code?: string;
  description?: string;
  status?: string;
  startDate?: string;
  endDate?: string;
  budget?: number;
  projectManagerId?: string;
  progress?: number;
}

export interface ProjectTaskDto {
  id: string;
  projectId?: string;
  projectName?: string;
  parentTaskId?: string;
  title: string;
  status?: string;
  priority?: string;
  startDate?: string;
  dueDate?: string;
  assignedToId?: string;
  assignedToName?: string;
  estimatedHours?: number;
  actualHours?: number;
  progress?: number;
}

export default api;
