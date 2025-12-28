import { useEffect, useState } from 'react';
import axios from 'axios';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faTrash, faTimes, faCheck, faChevronLeft, faChevronRight } from '@fortawesome/free-solid-svg-icons';
import { filesize } from 'filesize';

interface Suggestion {
  id: number;
  mediaType: string;
  mediaId: number;
  mediaTitle: string;
  mediaYear?: number;
  mediaSize: number;
  posterUrl?: string;
  ruleType: string;
  reason: string;
  dismissed: boolean;
  createdDate: string;
}

interface SuggestionBannerProps {
  mediaType: 'Movie' | 'Series';
  onDelete?: () => void;
}

const SuggestionBanner: React.FC<SuggestionBannerProps> = ({ mediaType, onDelete }) => {
  const [suggestions, setSuggestions] = useState<Suggestion[]>([]);
  const [currentIndex, setCurrentIndex] = useState(0);
  const [loading, setLoading] = useState(true);
  const [animationDirection, setAnimationDirection] = useState<'left' | 'right'>('right');

  const loadSuggestions = async () => {
    try {
      const response = await axios.get('/api/suggestion');
      const filtered = response.data.filter((s: Suggestion) => s.mediaType === mediaType);
      setSuggestions(filtered);
      setCurrentIndex(0);
    } catch (error) {
      console.error('Error loading suggestions:', error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadSuggestions();
  }, [mediaType]);

  const dismissSuggestion = async (id: number) => {
    try {
      await axios.post(`/api/suggestion/${id}/dismiss`);
      setSuggestions(suggestions.filter(s => s.id !== id));
      if (currentIndex >= suggestions.length - 1) {
        setCurrentIndex(Math.max(0, currentIndex - 1));
      }
    } catch (error) {
      console.error('Error dismissing suggestion:', error);
    }
  };

  const deleteSuggestion = async (suggestion: Suggestion) => {
    if (!confirm(`Are you sure you want to delete "${suggestion.mediaTitle}"?`)) {
      return;
    }

    try {
      await axios.post(`/api/suggestion/${suggestion.id}/dismiss`);
      
      const endpoint = suggestion.mediaType === 'Movie' 
        ? `/api/media/movie/${suggestion.mediaId}`
        : `/api/media/series/${suggestion.mediaId}`;
      
      await axios.delete(endpoint);
      
      setSuggestions(suggestions.filter(s => s.id !== suggestion.id));
      if (currentIndex >= suggestions.length - 1) {
        setCurrentIndex(Math.max(0, currentIndex - 1));
      }
      
      if (onDelete) onDelete();
    } catch (error) {
      console.error('Error deleting media:', error);
      alert('Error deleting media. Check console for details.');
    }
  };

  const getRuleTypeIcon = (ruleType: string) => {
    switch (ruleType) {
      case 'NotWatched': return '👁️';
      case 'FullyWatched': return '✓';
      case 'IgnoredRequest': return '📌';
      case 'UnmonitoredCleanup': return '🔕';
      default: return '💡';
    }
  };

  if (loading || suggestions.length === 0) {
    return null;
  }

  // Show 2 cards at a time
  const cardsToShow = 2;
  const totalPages = Math.ceil(suggestions.length / cardsToShow);
  const currentPage = Math.floor(currentIndex / cardsToShow);
  const startIdx = currentPage * cardsToShow;
  const endIdx = Math.min(startIdx + cardsToShow, suggestions.length);
  const visibleSuggestions = suggestions.slice(startIdx, endIdx);

  const nextPage = () => {
    const nextPageIdx = (currentPage + 1) % totalPages;
    setAnimationDirection('right');
    setCurrentIndex(nextPageIdx * cardsToShow);
  };

  const prevPage = () => {
    const prevPageIdx = (currentPage - 1 + totalPages) % totalPages;
    setAnimationDirection('left');
    setCurrentIndex(prevPageIdx * cardsToShow);
  };

  return (
    <div className="suggestion-banner">
      <div className="suggestion-banner-grid">
        {visibleSuggestions.map((suggestion) => (
          <div 
            key={suggestion.id} 
            className={`suggestion-banner-card slide-in-${animationDirection}`}
          >
            <div className="suggestion-banner-poster">
              {suggestion.posterUrl ? (
                <img src={suggestion.posterUrl} alt={suggestion.mediaTitle} />
              ) : (
                <div className="poster-placeholder-small">
                  {suggestion.mediaType === 'Movie' ? '🎬' : '📺'}
                </div>
              )}
            </div>

            <div className="suggestion-banner-details">
              <div className="suggestion-banner-header">
                <span className="suggestion-icon">{getRuleTypeIcon(suggestion.ruleType)}</span>
                <span className="suggestion-rule-name">{suggestion.ruleType}</span>
              </div>
              
              <div className="suggestion-banner-title">
                {suggestion.mediaTitle} {suggestion.mediaYear && `(${suggestion.mediaYear})`}
              </div>
              
              <div className="suggestion-banner-reason">{suggestion.reason}</div>
              
              <div className="suggestion-banner-meta">
                <span>{filesize(suggestion.mediaSize, { standard: 'jedec' })}</span>
              </div>
            </div>

            <div className="suggestion-banner-actions">
              <button 
                className="btn btn-danger btn-sm"
                onClick={() => deleteSuggestion(suggestion)}
                title="Delete this media"
              >
                <FontAwesomeIcon icon={faTrash} /> Delete
              </button>
              <button 
                className="btn btn-secondary btn-sm"
                onClick={() => dismissSuggestion(suggestion.id)}
                title="Keep but dismiss"
              >
                <FontAwesomeIcon icon={faCheck} /> Keep
              </button>
              <button 
                className="btn btn-secondary btn-sm"
                onClick={() => dismissSuggestion(suggestion.id)}
                title="Dismiss"
              >
                <FontAwesomeIcon icon={faTimes} /> Dismiss
              </button>
            </div>
          </div>
        ))}
      </div>

      {suggestions.length > cardsToShow && (
        <div className="suggestion-banner-footer">
          <button 
            className="nav-arrow"
            onClick={prevPage}
            title="Previous"
          >
            <FontAwesomeIcon icon={faChevronLeft} />
          </button>
          <span className="suggestion-pagination">
            {startIdx + 1}-{endIdx} of {suggestions.length} suggestions
          </span>
          <button 
            className="nav-arrow"
            onClick={nextPage}
            title="Next"
          >
            <FontAwesomeIcon icon={faChevronRight} />
          </button>
        </div>
      )}
    </div>
  );
};

export default SuggestionBanner;
