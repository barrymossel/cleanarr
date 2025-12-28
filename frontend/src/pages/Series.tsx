import { useEffect, useState, Fragment } from 'react';
import axios from 'axios';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faChevronDown, faChevronRight, faTrash, faSync, faSort, faSortUp, faSortDown, faUsers } from '@fortawesome/free-solid-svg-icons';
import { filesize } from 'filesize';
import SuggestionBanner from '../components/SuggestionBanner';

interface Episode {
  id: number;
  seasonNumber: number;
  episodeNumber: number;
  title: string;
  sizeOnDisk: number;
  airDate?: string;
  lastWatched?: string;
  watchedBy?: string;
  watchHistory?: string;  // JSON array
}

interface WatchEntry {
  user: string;
  date: string;
}

interface Series {
  id: number;
  title: string;
  year: number;
  added: string;
  requestedDate?: string;
  requestedBy?: string;
  totalSize: number;
  episodes: Episode[];
  monitored: boolean;
}

type SortField = 'title' | 'year' | 'totalSize' | 'added' | 'requestedDate' | 'status';
type SortDirection = 'asc' | 'desc';

const SeriesPage: React.FC = () => {
  const [seriesList, setSeriesList] = useState<Series[]>([]);
  const [loading, setLoading] = useState(true);
  const [expandedSeries, setExpandedSeries] = useState<Set<number>>(new Set());
  const [expandedSeasons, setExpandedSeasons] = useState<Set<string>>(new Set()); // Format: "seriesId-seasonNumber"
  const [notification, setNotification] = useState<{ message: string; type: 'success' | 'error' } | null>(null);
  const [sortField, setSortField] = useState<SortField>('added');
  const [sortDirection, setSortDirection] = useState<SortDirection>('desc');

  useEffect(() => {
    loadSeries();
  }, []);

  const loadSeries = async () => {
    try {
      const response = await axios.get('/api/media/series');
      setSeriesList(response.data);
      setLoading(false);
    } catch (error) {
      console.error('Error loading series:', error);
      setLoading(false);
    }
  };

  const getStatus = (series: Series): number => {
    // Return numeric value for sorting: 0=Missing, 1=Pending, 2=Unmonitored, 3=Available
    if (series.totalSize === 0 && !series.monitored) return 0; // Missing
    if (series.totalSize === 0 && series.monitored) return 1;  // Pending
    if (series.totalSize > 0 && !series.monitored) return 2;   // Unmonitored
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

  const renderWatchedBy = (episode: Episode) => {
    const history = parseWatchHistory(episode.watchHistory);
    const count = history.length;

    if (count === 0 && episode.watchedBy) {
      // Fallback to old data
      return <span>{episode.watchedBy}</span>;
    }

    if (count === 0) {
      return <span style={{ color: '#666' }}>-</span>;
    }

    if (count === 1) {
      return <span>{history[0].user}</span>;
    }

    // Multiple watchers
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

  const sortedSeries = [...seriesList].sort((a, b) => {
    if (sortField === 'status') {
      const aVal = getStatus(a);
      const bVal = getStatus(b);
      if (aVal < bVal) return sortDirection === 'asc' ? -1 : 1;
      if (aVal > bVal) return sortDirection === 'asc' ? 1 : -1;
      return 0;
    }

    let aVal: any = a[sortField];
    let bVal: any = b[sortField];

    if (sortField === 'added' || sortField === 'requestedDate') {
      aVal = aVal ? new Date(aVal).getTime() : 0;
      bVal = bVal ? new Date(bVal).getTime() : 0;
    }

    if (aVal < bVal) return sortDirection === 'asc' ? -1 : 1;
    if (aVal > bVal) return sortDirection === 'asc' ? 1 : -1;
    return 0;
  });

  const toggleExpand = (id: number) => {
    const newExpanded = new Set(expandedSeries);
    if (newExpanded.has(id)) {
      newExpanded.delete(id);
    } else {
      newExpanded.add(id);
    }
    setExpandedSeries(newExpanded);
  };

  const toggleSeason = (seriesId: number, seasonNumber: number, event: React.MouseEvent) => {
    event.stopPropagation(); // Prevent series row from toggling
    const key = `${seriesId}-${seasonNumber}`;
    const newExpanded = new Set(expandedSeasons);
    if (newExpanded.has(key)) {
      newExpanded.delete(key);
    } else {
      newExpanded.add(key);
    }
    setExpandedSeasons(newExpanded);
  };

  const handleDeleteSeries = async (id: number, title: string) => {
    if (!window.confirm(`Are you sure you want to delete "${title}"? This will delete all episodes.`)) {
      return;
    }

    try {
      await axios.delete(`/api/media/series/${id}`);
      setSeriesList(seriesList.filter(s => s.id !== id));
      showNotification('Series deleted successfully', 'success');
    } catch (error: any) {
      showNotification(error.response?.data?.message || 'Error deleting series', 'error');
    }
  };

  const handleDeleteEpisode = async (seriesId: number, episodeId: number, title: string) => {
    if (!window.confirm(`Are you sure you want to delete episode "${title}"? This will delete the file from disk.`)) {
      return;
    }

    try {
      await axios.delete(`/api/media/episode/${episodeId}`);
      
      setSeriesList(seriesList.map(s => {
        if (s.id === seriesId) {
          return {
            ...s,
            episodes: s.episodes.filter(e => e.id !== episodeId)
          };
        }
        return s;
      }));

      showNotification('Episode deleted successfully', 'success');
    } catch (error: any) {
      showNotification(error.response?.data?.message || 'Error deleting episode', 'error');
    }
  };

  const handleSync = async () => {
    try {
      await axios.post('/api/media/sync');
      showNotification('Sync started', 'success');
      setTimeout(loadSeries, 2000);
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

  const groupEpisodesBySeason = (episodes: Episode[]) => {
    const grouped: { [key: number]: Episode[] } = {};
    episodes.forEach(ep => {
      if (!grouped[ep.seasonNumber]) {
        grouped[ep.seasonNumber] = [];
      }
      grouped[ep.seasonNumber].push(ep);
    });
    return grouped;
  };

  if (loading) {
    return <div className="loading">Loading series...</div>;
  }

  return (
    <div>
      <div className="page-header">
        <h1 className="page-title">Series</h1>
        <button className="btn btn-primary" onClick={handleSync}>
          <FontAwesomeIcon icon={faSync} /> Sync Now
        </button>
      </div>

      <SuggestionBanner mediaType="Series" onDelete={loadSeries} />

      {seriesList.length === 0 ? (
        <div className="empty-state">
          No series found. Configure your settings and sync to see series.
        </div>
      ) : (
        <div className="table-container">
          <table>
            <thead>
              <tr>
                <th style={{ width: '30px' }}></th>
                <th onClick={() => handleSort('title')}>
                  Title {getSortIcon('title')}
                </th>
                <th onClick={() => handleSort('year')}>
                  Year {getSortIcon('year')}
                </th>
                <th onClick={() => handleSort('totalSize')}>
                  Total Size {getSortIcon('totalSize')}
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
                <th className="action-cell">Actions</th>
              </tr>
            </thead>
            <tbody>
              {sortedSeries.map(series => (
                <Fragment key={series.id}>
                  <tr className="expandable-row" onClick={() => toggleExpand(series.id)}>
                    <td>
                      <FontAwesomeIcon 
                        icon={expandedSeries.has(series.id) ? faChevronDown : faChevronRight} 
                      />
                    </td>
                    <td>{series.title}</td>
                    <td>{series.year}</td>
                    <td>{filesize(series.totalSize)}</td>
                    <td>
                      {series.totalSize === 0 && !series.monitored && (
                        <span className="badge badge-warning" title="Missing - Not monitored in Sonarr">
                          ⚠️ Missing
                        </span>
                      )}
                      {series.totalSize === 0 && series.monitored && (
                        <span className="badge badge-info" title="Monitored - Waiting for download">
                          ⏳ Pending
                        </span>
                      )}
                      {series.totalSize > 0 && !series.monitored && (
                        <span className="badge badge-secondary" title="Not monitored in Sonarr">
                          🔕 Unmonitored
                        </span>
                      )}
                      {series.totalSize > 0 && series.monitored && (
                        <span className="badge badge-success" title="Downloaded and monitored">
                          ✅ Available
                        </span>
                      )}
                    </td>
                    <td className="date-cell">{formatDate(series.added)}</td>
                    <td className="date-cell">{formatDate(series.requestedDate)}</td>
                    <td>{series.requestedBy || '-'}</td>
                    <td className="action-cell" onClick={(e) => e.stopPropagation()}>
                      <button 
                        className="btn btn-danger" 
                        onClick={() => handleDeleteSeries(series.id, series.title)}
                      >
                        <FontAwesomeIcon icon={faTrash} />
                      </button>
                    </td>
                  </tr>
                  {expandedSeries.has(series.id) && (
                    <tr>
                      <td colSpan={9}>
                        <div className="expanded-content">
                          {Object.entries(groupEpisodesBySeason(series.episodes))
                            .sort(([a], [b]) => parseInt(a) - parseInt(b))
                            .map(([seasonNum, episodes]) => {
                              const seasonKey = `${series.id}-${seasonNum}`;
                              const isSeasonExpanded = expandedSeasons.has(seasonKey);
                              
                              return (
                                <div key={seasonNum} className="season-group">
                                  <div 
                                    className="season-header" 
                                    onClick={(e) => toggleSeason(series.id, parseInt(seasonNum), e)}
                                    style={{ cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '8px' }}
                                  >
                                    <FontAwesomeIcon 
                                      icon={isSeasonExpanded ? faChevronDown : faChevronRight} 
                                    />
                                    <span>Season {seasonNum}</span>
                                  </div>
                                  {isSeasonExpanded && (
                                    <table className="episode-table">
                                      <thead>
                                        <tr>
                                          <th>Episode</th>
                                          <th>Title</th>
                                          <th>Size</th>
                                          <th>Air Date</th>
                                          <th>Last Watched</th>
                                          <th>Watched By</th>
                                          <th className="action-cell">Actions</th>
                                        </tr>
                                      </thead>
                                      <tbody>
                                        {episodes
                                          .sort((a, b) => a.episodeNumber - b.episodeNumber)
                                          .map(episode => (
                                            <tr key={episode.id}>
                                              <td>E{episode.episodeNumber}</td>
                                              <td>{episode.title}</td>
                                              <td className="size-cell">{filesize(episode.sizeOnDisk)}</td>
                                              <td className="date-cell">{formatDate(episode.airDate)}</td>
                                              <td className="date-cell">{formatDate(episode.lastWatched)}</td>
                                              <td>{renderWatchedBy(episode)}</td>
                                              <td className="action-cell">
                                                <button 
                                                  className="btn btn-danger" 
                                                  onClick={() => handleDeleteEpisode(series.id, episode.id, episode.title)}
                                                >
                                                  <FontAwesomeIcon icon={faTrash} />
                                                </button>
                                              </td>
                                            </tr>
                                          ))}
                                      </tbody>
                                    </table>
                                  )}
                                </div>
                              );
                            })}
                        </div>
                      </td>
                    </tr>
                  )}
                </Fragment>
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

export default SeriesPage;
