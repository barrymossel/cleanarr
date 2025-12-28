import { useEffect, useState } from 'react';
import axios from 'axios';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faSort, faSortUp, faSortDown, faTrash, faSync, faUsers } from '@fortawesome/free-solid-svg-icons';
import { filesize } from 'filesize';
import SuggestionBanner from '../components/SuggestionBanner';

interface Movie {
  id: number;
  title: string;
  year: number;
  sizeOnDisk: number;
  added: string;
  requestedDate?: string;
  requestedBy?: string;
  lastWatched?: string;
  watchedBy?: string;
  watchHistory?: string;  // JSON array
  monitored: boolean;
}

interface WatchEntry {
  user: string;
  date: string;
}

type SortField = 'title' | 'year' | 'sizeOnDisk' | 'added' | 'requestedDate' | 'lastWatched' | 'status';
type SortDirection = 'asc' | 'desc';

const Movies: React.FC = () => {
  const [movies, setMovies] = useState<Movie[]>([]);
  const [loading, setLoading] = useState(true);
  const [sortField, setSortField] = useState<SortField>('added');
  const [sortDirection, setSortDirection] = useState<SortDirection>('desc');
  const [notification, setNotification] = useState<{ message: string; type: 'success' | 'error' } | null>(null);

  useEffect(() => {
    loadMovies();
  }, []);

  const loadMovies = async () => {
    try {
      const response = await axios.get('/api/media/movies');
      setMovies(response.data);
      setLoading(false);
    } catch (error) {
      console.error('Error loading movies:', error);
      setLoading(false);
    }
  };

  const getStatus = (movie: Movie): number => {
    // Return numeric value for sorting: 0=Missing, 1=Pending, 2=Unmonitored, 3=Available
    if (movie.sizeOnDisk === 0 && !movie.monitored) return 0; // Missing
    if (movie.sizeOnDisk === 0 && movie.monitored) return 1;  // Pending
    if (movie.sizeOnDisk > 0 && !movie.monitored) return 2;   // Unmonitored
    return 3; // Available
  };

  const parseWatchHistory = (watchHistory?: string): WatchEntry[] => {
    if (!watchHistory) return [];
    try {
      return JSON.parse(watchHistory);
    } catch {
      return [];
    }
  };

  const formatDateDutch = (dateStr: string): string => {
    const date = new Date(dateStr);
    const day = String(date.getDate()).padStart(2, '0');
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const year = date.getFullYear();
    return `${day}-${month}-${year}`;
  };

  const renderWatchedBy = (movie: Movie) => {
    const history = parseWatchHistory(movie.watchHistory);
    const count = history.length;

    if (count === 0 && movie.watchedBy) {
      // Fallback to old data (no history yet)
      return <span>{movie.watchedBy}</span>;
    }

    if (count === 0) {
      return <span style={{ color: '#666' }}>-</span>;
    }

    if (count === 1) {
      return <span>{history[0].user}</span>;
    }

    // Multiple watchers - show last person + badge
    const lastWatch = history[history.length - 1];
    const tooltipContent = history
      .map((w, idx) => `${w.user} - ${formatDateDutch(w.date)}${idx === history.length - 1 ? ' ✓' : ''}`)
      .join('\n');

    return (
      <span title={tooltipContent} style={{ cursor: 'pointer' }}>
        {lastWatch.user} <span style={{ 
          backgroundColor: '#5d9cec', 
          color: 'white', 
          padding: '2px 6px', 
          borderRadius: '10px', 
          fontSize: '0.85em',
          marginLeft: '4px'
        }}><FontAwesomeIcon icon={faUsers} /> {count}</span>
      </span>
    );
  };

  const handleSort = (field: SortField) => {
    if (sortField === field) {
      setSortDirection(sortDirection === 'asc' ? 'desc' : 'asc');
    } else {
      setSortField(field);
      setSortDirection('asc');
    }
  };

  const getSortIcon = (field: SortField) => {
    if (sortField !== field) return <FontAwesomeIcon icon={faSort} className="sort-icon" />;
    return sortDirection === 'asc' 
      ? <FontAwesomeIcon icon={faSortUp} className="sort-icon" />
      : <FontAwesomeIcon icon={faSortDown} className="sort-icon" />;
  };

  const sortedMovies = [...movies].sort((a, b) => {
    if (sortField === 'status') {
      const aVal = getStatus(a);
      const bVal = getStatus(b);
      if (aVal < bVal) return sortDirection === 'asc' ? -1 : 1;
      if (aVal > bVal) return sortDirection === 'asc' ? 1 : -1;
      return 0;
    }

    let aVal: any = a[sortField];
    let bVal: any = b[sortField];

    if (sortField === 'added' || sortField === 'requestedDate' || sortField === 'lastWatched') {
      aVal = aVal ? new Date(aVal).getTime() : 0;
      bVal = bVal ? new Date(bVal).getTime() : 0;
    }

    if (aVal < bVal) return sortDirection === 'asc' ? -1 : 1;
    if (aVal > bVal) return sortDirection === 'asc' ? 1 : -1;
    return 0;
  });

  const handleDelete = async (id: number, title: string) => {
    if (!window.confirm(`Are you sure you want to delete "${title}"? This will also delete the files from disk.`)) {
      return;
    }

    try {
      await axios.delete(`/api/media/movie/${id}`);
      setMovies(movies.filter(m => m.id !== id));
      showNotification('Movie deleted successfully', 'success');
    } catch (error: any) {
      showNotification(error.response?.data?.message || 'Error deleting movie', 'error');
    }
  };

  const handleSync = async () => {
    try {
      await axios.post('/api/media/sync');
      showNotification('Sync started', 'success');
      setTimeout(loadMovies, 2000);
    } catch (error) {
      showNotification('Error starting sync', 'error');
    }
  };

  const showNotification = (message: string, type: 'success' | 'error') => {
    setNotification({ message, type });
    setTimeout(() => setNotification(null), 3000);
  };

  const formatDate = (date?: string) => {
    if (!date) return '-';
    return new Date(date).toLocaleDateString();
  };

  if (loading) {
    return <div className="loading">Loading movies...</div>;
  }

  return (
    <div>
      <div className="page-header">
        <h1 className="page-title">Movies</h1>
        <button className="btn btn-primary" onClick={handleSync}>
          <FontAwesomeIcon icon={faSync} /> Sync Now
        </button>
      </div>

      <SuggestionBanner mediaType="Movie" onDelete={loadMovies} />

      {movies.length === 0 ? (
        <div className="empty-state">
          No movies found. Configure your settings and sync to see movies.
        </div>
      ) : (
        <div className="table-container">
          <table>
            <thead>
              <tr>
                <th onClick={() => handleSort('title')}>
                  Title {getSortIcon('title')}
                </th>
                <th onClick={() => handleSort('year')}>
                  Year {getSortIcon('year')}
                </th>
                <th onClick={() => handleSort('sizeOnDisk')}>
                  Size {getSortIcon('sizeOnDisk')}
                </th>
                <th onClick={() => handleSort('status')}>
                  Status {getSortIcon('status')}
                </th>
                <th onClick={() => handleSort('added')}>
                  Added {getSortIcon('added')}
                </th>
                <th onClick={() => handleSort('requestedDate')}>
                  Requested {getSortIcon('requestedDate')}
                </th>
                <th>Requested By</th>
                <th onClick={() => handleSort('lastWatched')}>
                  Last Watched {getSortIcon('lastWatched')}
                </th>
                <th>Watched By</th>
                <th className="action-cell">Actions</th>
              </tr>
            </thead>
            <tbody>
              {sortedMovies.map(movie => (
                <tr key={movie.id}>
                  <td>{movie.title}</td>
                  <td>{movie.year}</td>
                  <td className="size-cell">{filesize(movie.sizeOnDisk)}</td>
                  <td>
                    {movie.sizeOnDisk === 0 && !movie.monitored && (
                      <span className="badge badge-warning" title="Missing - Not monitored in Radarr">
                        ⚠️ Missing
                      </span>
                    )}
                    {movie.sizeOnDisk === 0 && movie.monitored && (
                      <span className="badge badge-info" title="Monitored - Waiting for download">
                        ⏳ Pending
                      </span>
                    )}
                    {movie.sizeOnDisk > 0 && !movie.monitored && (
                      <span className="badge badge-secondary" title="Not monitored in Radarr">
                        🔕 Unmonitored
                      </span>
                    )}
                    {movie.sizeOnDisk > 0 && movie.monitored && (
                      <span className="badge badge-success" title="Downloaded and monitored">
                        ✅ Available
                      </span>
                    )}
                  </td>
                  <td className="date-cell">{formatDate(movie.added)}</td>
                  <td className="date-cell">{formatDate(movie.requestedDate)}</td>
                  <td>{movie.requestedBy || '-'}</td>
                  <td className="date-cell">{formatDate(movie.lastWatched)}</td>
                  <td>{renderWatchedBy(movie)}</td>
                  <td className="action-cell">
                    <button 
                      className="btn btn-danger" 
                      onClick={() => handleDelete(movie.id, movie.title)}
                    >
                      <FontAwesomeIcon icon={faTrash} />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {notification && (
        <div className={`notification ${notification.type}`}>
          {notification.message}
        </div>
      )}
    </div>
  );
};

export default Movies;
