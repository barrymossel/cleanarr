import { Link, useLocation } from 'react-router-dom';
import { useState } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faFilm, faTv, faGear, faCog, faBars, faTimes } from '@fortawesome/free-solid-svg-icons';

interface LayoutProps {
  children: React.ReactNode;
}

const Layout: React.FC<LayoutProps> = ({ children }) => {
  const location = useLocation();
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);

  return (
    <div className="app-container">
      <aside className={`sidebar ${sidebarCollapsed ? 'collapsed' : ''}`}>
        <div className="sidebar-header">
          <Link to="/" className="sidebar-logo">
            {!sidebarCollapsed && 'Cleanarr'}
          </Link>
          <button 
            className="sidebar-toggle"
            onClick={() => setSidebarCollapsed(!sidebarCollapsed)}
            title={sidebarCollapsed ? 'Expand sidebar' : 'Collapse sidebar'}
          >
            <FontAwesomeIcon icon={sidebarCollapsed ? faBars : faTimes} />
          </button>
        </div>
        
        <nav className="sidebar-nav">
          <Link 
            to="/movies" 
            className={`sidebar-item ${location.pathname === '/movies' ? 'active' : ''}`}
            title="Movies"
          >
            <FontAwesomeIcon icon={faFilm} />
            {!sidebarCollapsed && <span>Movies</span>}
          </Link>
          <Link 
            to="/series" 
            className={`sidebar-item ${location.pathname === '/series' ? 'active' : ''}`}
            title="Series"
          >
            <FontAwesomeIcon icon={faTv} />
            {!sidebarCollapsed && <span>Series</span>}
          </Link>
          <Link 
            to="/suggestions" 
            className={`sidebar-item ${location.pathname === '/suggestions' ? 'active' : ''}`}
            title="Suggestion Rules"
          >
            <FontAwesomeIcon icon={faGear} />
            {!sidebarCollapsed && <span>Rules</span>}
          </Link>
          <Link 
            to="/settings" 
            className={`sidebar-item ${location.pathname === '/settings' ? 'active' : ''}`}
            title="Settings"
          >
            <FontAwesomeIcon icon={faCog} />
            {!sidebarCollapsed && <span>Settings</span>}
          </Link>
        </nav>
      </aside>
      
      <main className={`main-content ${sidebarCollapsed ? 'sidebar-collapsed' : ''}`}>
        {children}
      </main>
    </div>
  );
};

export default Layout;
