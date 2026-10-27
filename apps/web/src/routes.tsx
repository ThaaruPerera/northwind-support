import { createBrowserRouter, Navigate, type RouteObject } from 'react-router-dom';
import { App } from './App';
import { RequireAuth } from './auth/RequireAuth';
import { WithAuth } from './auth/WithAuth';
import { NotFoundPage } from './features/NotFoundPage';
import { LoginPage } from './features/auth/LoginPage';
import { CustomerDetailPage } from './features/customers/CustomerDetailPage';
import { CustomerListPage } from './features/customers/CustomerListPage';
import { CreateTicketPage } from './features/tickets/CreateTicketPage';
import { TicketDetailPage } from './features/tickets/TicketDetailPage';
import { TicketListPage } from './features/tickets/TicketListPage';

/**
 * Every page except the login page sits behind <RequireAuth>, so a page added later is protected
 * unless someone goes out of their way to put it elsewhere.
 */
export const routeConfig: RouteObject[] = [
  {
    element: <WithAuth />,
    children: [
      { path: '/login', element: <LoginPage /> },
      {
        element: <RequireAuth />,
        children: [
          {
            path: '/',
            element: <App />,
            children: [
              { index: true, element: <Navigate to="/tickets" replace /> },
              { path: 'tickets', element: <TicketListPage /> },
              { path: 'tickets/new', element: <CreateTicketPage /> },
              { path: 'tickets/:id', element: <TicketDetailPage /> },
              { path: 'customers', element: <CustomerListPage /> },
              { path: 'customers/:id', element: <CustomerDetailPage /> },
              { path: '*', element: <NotFoundPage /> },
            ],
          },
        ],
      },
    ],
  },
];

export const router = createBrowserRouter(routeConfig);
