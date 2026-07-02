import { useEffect } from 'react';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AuthProvider } from '@/full/context/AuthContext';
import { ProtectedRoute } from '@/full/components/auth/ProtectedRoute';
import { FullLayout } from '@/full/components/layout/FullLayout';
import { seedGuestTestsIfEmpty } from '@/guest/bootstrap/seedLocalTests';
import { HomePage } from '@/full/pages/HomePage';
import { LoginPage } from '@/full/pages/LoginPage';
import { MyTestsPage } from '@/full/pages/MyTestsPage';
import { ProfilePage } from '@/full/pages/ProfilePage';
import { PublicTestsPage } from '@/full/pages/PublicTestsPage';
import { RegisterPage } from '@/full/pages/RegisterPage';
import { PlayPage as ApiPlayPage } from '@/full/pages/PlayPage';
import { ApplicationPlayPage } from '@/full/pages/ApplicationPlayPage';
import { ApplicationsPage } from '@/full/pages/ApplicationsPage';
import { EditorPage } from '@/guest/pages/EditorPage';
import { ImportPage } from '@/guest/pages/ImportPage';
import { InfoPage } from '@/guest/pages/InfoPage';
import { PlayPage as LocalPlayPage } from '@/guest/pages/PlayPage';
import { routerBasename } from '@/utils/router';

export function FullApp() {
  useEffect(() => {
    seedGuestTestsIfEmpty();
  }, []);

  return (
    <AuthProvider>
      <BrowserRouter basename={routerBasename()}>
        <Routes>
          <Route element={<FullLayout />}>
            <Route index element={<HomePage />} />
            <Route path="login" element={<LoginPage />} />
            <Route path="register" element={<RegisterPage />} />
            <Route path="tests" element={<PublicTestsPage />} />
            <Route path="tests/:id/play" element={<ApiPlayPage />} />
            <Route path="application/:token" element={<ApplicationPlayPage />} />
            <Route path="play/:id" element={<LocalPlayPage />} />
            <Route path="editor" element={<EditorPage />} />
            <Route path="editor/:id" element={<EditorPage />} />
            <Route path="import" element={<ImportPage />} />
            <Route path="info" element={<InfoPage />} />
            <Route path="local/tests" element={<Navigate to="/my/tests" replace />} />
            <Route path="my/tests" element={<MyTestsPage />} />
            <Route element={<ProtectedRoute />}>
              <Route path="profile" element={<ProfilePage />} />
              <Route path="applications" element={<ApplicationsPage />} />
            </Route>
            <Route path="*" element={<Navigate to="/" replace />} />
          </Route>
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}
