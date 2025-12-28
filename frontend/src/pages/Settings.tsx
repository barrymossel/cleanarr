import { useEffect, useState } from 'react';
import axios from 'axios';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faSave, faCheck, faTimes } from '@fortawesome/free-solid-svg-icons';

interface Settings {
  RadarrUrl: string;
  RadarrApiKey: string;
  SonarrUrl: string;
  SonarrApiKey: string;
  TautulliUrl: string;
  TautulliApiKey: string;
  OverseerrUrl: string;
  OverseerrApiKey: string;
  CronSchedule: string;
}

type TestStatus = 'idle' | 'testing' | 'success' | 'error';

interface VersionInfo {
  version: string;
  buildDate: string;
}

const SettingsPage: React.FC = () => {
  const [settings, setSettings] = useState<Settings>({
    RadarrUrl: '',
    RadarrApiKey: '',
    SonarrUrl: '',
    SonarrApiKey: '',
    TautulliUrl: '',
    TautulliApiKey: '',
    OverseerrUrl: '',
    OverseerrApiKey: '',
    CronSchedule: '0 */6 * * *'
  });

  const [versionInfo, setVersionInfo] = useState<VersionInfo | null>(null);

  const [testStatus, setTestStatus] = useState<{ [key: string]: TestStatus }>({
    radarr: 'idle',
    sonarr: 'idle',
    tautulli: 'idle',
    overseerr: 'idle'
  });

  const [notification, setNotification] = useState<{ message: string; type: 'success' | 'error' } | null>(null);

  useEffect(() => {
    loadSettings();
    loadVersion();
  }, []);

  const loadSettings = async () => {
    try {
      const response = await axios.get('/api/settings');
      setSettings(response.data);
    } catch (error) {
      console.error('Error loading settings:', error);
    }
  };

  const loadVersion = async () => {
    try {
      const response = await axios.get('/api/settings/version');
      setVersionInfo(response.data);
    } catch (error) {
      console.error('Error loading version:', error);
    }
  };

  const handleChange = (field: keyof Settings, value: string) => {
    setSettings({ ...settings, [field]: value });
  };

  const handleSave = async () => {
    try {
      await axios.post('/api/settings', settings);
      showNotification('Settings saved successfully', 'success');
    } catch (error) {
      showNotification('Error saving settings', 'error');
    }
  };

  const testConnection = async (service: string) => {
    setTestStatus({ ...testStatus, [service]: 'testing' });

    try {
      await axios.post(`/api/settings/test/${service}`);
      setTestStatus({ ...testStatus, [service]: 'success' });
      setTimeout(() => {
        setTestStatus({ ...testStatus, [service]: 'idle' });
      }, 3000);
    } catch (error: any) {
      setTestStatus({ ...testStatus, [service]: 'error' });
      showNotification(error.response?.data?.message || `Failed to connect to ${service}`, 'error');
      setTimeout(() => {
        setTestStatus({ ...testStatus, [service]: 'idle' });
      }, 3000);
    }
  };

  const showNotification = (message: string, type: 'success' | 'error') => {
    setNotification({ message, type });
    setTimeout(() => setNotification(null), 3000);
  };

  const renderTestButton = (service: string, label: string) => {
    const status = testStatus[service];
    
    return (
      <div className="test-connection">
        <button 
          className="btn btn-primary" 
          onClick={() => testConnection(service)}
          disabled={status === 'testing'}
        >
          {status === 'testing' ? 'Testing...' : `Test ${label}`}
        </button>
        {status === 'success' && (
          <span className="test-status success">
            <FontAwesomeIcon icon={faCheck} /> Connected
          </span>
        )}
        {status === 'error' && (
          <span className="test-status error">
            <FontAwesomeIcon icon={faTimes} /> Failed
          </span>
        )}
      </div>
    );
  };

  return (
    <div>
      <div className="page-header">
        <h1 className="page-title">Settings</h1>
      </div>

      <div className="settings-form">
        <h2>Radarr</h2>
        <div className="form-group">
          <label className="form-label">URL</label>
          <input
            type="text"
            className="form-input"
            placeholder="http://localhost:7878"
            value={settings.RadarrUrl}
            onChange={(e) => handleChange('RadarrUrl', e.target.value)}
          />
        </div>
        <div className="form-group">
          <label className="form-label">API Key</label>
          <input
            type="text"
            className="form-input"
            placeholder="Your Radarr API Key"
            value={settings.RadarrApiKey}
            onChange={(e) => handleChange('RadarrApiKey', e.target.value)}
          />
          {renderTestButton('radarr', 'Radarr')}
        </div>

        <h2 style={{ marginTop: '30px' }}>Sonarr</h2>
        <div className="form-group">
          <label className="form-label">URL</label>
          <input
            type="text"
            className="form-input"
            placeholder="http://localhost:8989"
            value={settings.SonarrUrl}
            onChange={(e) => handleChange('SonarrUrl', e.target.value)}
          />
        </div>
        <div className="form-group">
          <label className="form-label">API Key</label>
          <input
            type="text"
            className="form-input"
            placeholder="Your Sonarr API Key"
            value={settings.SonarrApiKey}
            onChange={(e) => handleChange('SonarrApiKey', e.target.value)}
          />
          {renderTestButton('sonarr', 'Sonarr')}
        </div>

        <h2 style={{ marginTop: '30px' }}>Tautulli</h2>
        <div className="form-group">
          <label className="form-label">URL</label>
          <input
            type="text"
            className="form-input"
            placeholder="http://localhost:8181"
            value={settings.TautulliUrl}
            onChange={(e) => handleChange('TautulliUrl', e.target.value)}
          />
        </div>
        <div className="form-group">
          <label className="form-label">API Key</label>
          <input
            type="text"
            className="form-input"
            placeholder="Your Tautulli API Key"
            value={settings.TautulliApiKey}
            onChange={(e) => handleChange('TautulliApiKey', e.target.value)}
          />
          {renderTestButton('tautulli', 'Tautulli')}
        </div>

        <h2 style={{ marginTop: '30px' }}>Overseerr</h2>
        <div className="form-group">
          <label className="form-label">URL</label>
          <input
            type="text"
            className="form-input"
            placeholder="http://localhost:5055"
            value={settings.OverseerrUrl}
            onChange={(e) => handleChange('OverseerrUrl', e.target.value)}
          />
        </div>
        <div className="form-group">
          <label className="form-label">API Key</label>
          <input
            type="text"
            className="form-input"
            placeholder="Your Overseerr API Key"
            value={settings.OverseerrApiKey}
            onChange={(e) => handleChange('OverseerrApiKey', e.target.value)}
          />
          {renderTestButton('overseerr', 'Overseerr')}
        </div>

        <h2 style={{ marginTop: '30px' }}>Sync Schedule</h2>
        <div className="form-group">
          <label className="form-label">Cron Expression</label>
          <input
            type="text"
            className="form-input"
            placeholder="0 */6 * * *"
            value={settings.CronSchedule}
            onChange={(e) => handleChange('CronSchedule', e.target.value)}
          />
          <small style={{ color: '#888', display: 'block', marginTop: '5px' }}>
            Default: 0 */6 * * * (every 6 hours). Format: minute hour day month weekday
          </small>
        </div>

        <div className="form-actions">
          <button className="btn btn-success" onClick={handleSave}>
            <FontAwesomeIcon icon={faSave} /> Save Settings
          </button>
        </div>

        <div style={{ 
          marginTop: '40px', 
          paddingTop: '20px', 
          borderTop: '1px solid #333',
          color: '#888',
          fontSize: '0.9em',
          display: versionInfo ? 'block' : 'none'
        }}>
          <div><strong>Version:</strong> {versionInfo?.version || 'unknown'}</div>
          <div><strong>Build Date:</strong> {versionInfo?.buildDate || 'unknown'}</div>
        </div>
      </div>

      {notification && (
        <div className={`notification ${notification.type}`}>
          {notification.message}
        </div>
      )}
    </div>
  );
};

export default SettingsPage;
