import { useEffect, useState } from 'react';
import axios from 'axios';

interface SuggestionRule {
  id: number;
  name: string;
  type: string;
  enabled: boolean;
  daysThreshold: number;
  countThreshold?: number;
  applyToMovies: boolean;
  applyToSeries: boolean;
}

const SuggestionRules = () => {
  const [rules, setRules] = useState<SuggestionRule[]>([]);
  const [loading, setLoading] = useState(true);

  const loadRules = async () => {
    try {
      const response = await axios.get('/api/suggestion/rules');
      setRules(response.data);
    } catch (error) {
      console.error('Error loading rules:', error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadRules();
  }, []);

  const updateRule = async (rule: SuggestionRule) => {
    try {
      await axios.put(`/api/suggestion/rules/${rule.id}`, rule);
      alert('Rule updated! Suggestions will be regenerated.');
    } catch (error) {
      console.error('Error updating rule:', error);
      alert('Error updating rule');
    }
  };

  const getRuleDescription = (rule: SuggestionRule) => {
    switch (rule.type) {
      case 'NotWatched':
        return `Suggest deletion if not watched for ${rule.daysThreshold} days`;
      case 'FullyWatched':
        return `Suggest deletion if watched by ${rule.countThreshold || 2}+ people`;
      case 'IgnoredRequest':
        return `Suggest deletion if requested but not watched for ${rule.daysThreshold} days`;
      case 'UnmonitoredCleanup':
        return `Suggest deletion if unmonitored for ${rule.daysThreshold}+ days`;
      default:
        return rule.name;
    }
  };

  if (loading) {
    return <div>Loading rules...</div>;
  }

  return (
    <div className="rules-container">
      <h2>Suggestion Rules</h2>
      <p style={{ color: '#888', marginBottom: '20px' }}>
        Configure when Cleanarr should suggest media for deletion. Suggestions are generated after each sync.
      </p>

      <div className="rules-list">
        {rules.map(rule => (
          <div key={rule.id} className="rule-card">
            <div className="rule-header">
              <label className="rule-toggle">
                <input
                  type="checkbox"
                  checked={rule.enabled}
                  onChange={(e) => {
                    const updated = { ...rule, enabled: e.target.checked };
                    setRules(rules.map(r => r.id === rule.id ? updated : r));
                    updateRule(updated);
                  }}
                />
                <span className="rule-name">{rule.name}</span>
              </label>
            </div>

            <div className="rule-description">
              {getRuleDescription(rule)}
            </div>

            <div className="rule-options">
              {rule.type === 'NotWatched' || rule.type === 'IgnoredRequest' || rule.type === 'UnmonitoredCleanup' ? (
                <label>
                  Days threshold:
                  <input
                    type="number"
                    min="1"
                    value={rule.daysThreshold}
                    onChange={(e) => {
                      const updated = { ...rule, daysThreshold: parseInt(e.target.value) || 1 };
                      setRules(rules.map(r => r.id === rule.id ? updated : r));
                    }}
                    onBlur={() => updateRule(rule)}
                    style={{ width: '80px', marginLeft: '10px' }}
                  />
                </label>
              ) : null}

              {rule.type === 'FullyWatched' ? (
                <label>
                  Watch count threshold:
                  <input
                    type="number"
                    min="1"
                    value={rule.countThreshold || 2}
                    onChange={(e) => {
                      const updated = { ...rule, countThreshold: parseInt(e.target.value) || 2 };
                      setRules(rules.map(r => r.id === rule.id ? updated : r));
                    }}
                    onBlur={() => updateRule(rule)}
                    style={{ width: '80px', marginLeft: '10px' }}
                  />
                </label>
              ) : null}

              <label style={{ marginLeft: '20px' }}>
                <input
                  type="checkbox"
                  checked={rule.applyToMovies}
                  onChange={(e) => {
                    const updated = { ...rule, applyToMovies: e.target.checked };
                    setRules(rules.map(r => r.id === rule.id ? updated : r));
                    updateRule(updated);
                  }}
                />
                {' '}Movies
              </label>

              <label style={{ marginLeft: '10px' }}>
                <input
                  type="checkbox"
                  checked={rule.applyToSeries}
                  onChange={(e) => {
                    const updated = { ...rule, applyToSeries: e.target.checked };
                    setRules(rules.map(r => r.id === rule.id ? updated : r));
                    updateRule(updated);
                  }}
                />
                {' '}Series
              </label>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};

export default SuggestionRules;
