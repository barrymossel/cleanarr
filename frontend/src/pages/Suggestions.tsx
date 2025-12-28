import { useEffect, useState } from 'react';
import axios from 'axios';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faPlus, faTrash, faSave } from '@fortawesome/free-solid-svg-icons';

interface Condition {
  field: string;
  operator: string;
  value: string;
  valueType: string;
  logicalOperator: string | null;
}

interface SuggestionRule {
  id: number;
  name: string;
  description: string;
  enabled: boolean;
  applyToMovies: boolean;
  applyToSeries: boolean;
  conditionsJson: string;
  isCustom: boolean;
}

const FIELDS = [
  { value: 'lastWatched', label: 'Last Watched', type: 'date' },
  { value: 'added', label: 'Added Date', type: 'date' },
  { value: 'requestedDate', label: 'Requested Date', type: 'date' },
  { value: 'requestedBy', label: 'Requested By', type: 'text' },
  { value: 'watchedBy', label: 'Watched By', type: 'text' },
  { value: 'watchCount', label: 'Watch Count', type: 'number' },
  { value: 'sizeOnDisk', label: 'Size on Disk (bytes)', type: 'number' },
  { value: 'totalSize', label: 'Total Size (bytes)', type: 'number' },
  { value: 'year', label: 'Year', type: 'number' },
  { value: 'monitored', label: 'Monitored', type: 'boolean' },
  { value: 'title', label: 'Title', type: 'text' },
  { value: 'quality', label: 'Quality', type: 'text' },
];

const OPERATORS = {
  number: [
    { value: 'bigger', label: '>' },
    { value: 'smaller', label: '<' },
    { value: 'equals', label: '=' },
    { value: 'not_equals', label: '≠' },
  ],
  text: [
    { value: 'equals', label: 'equals' },
    { value: 'not_equals', label: 'not equals' },
    { value: 'contains', label: 'contains' },
    { value: 'contains_partial', label: 'contains (partial)' },
    { value: 'not_contains', label: 'doesn\'t contain' },
    { value: 'not_contains_partial', label: 'doesn\'t contain (partial)' },
  ],
  date: [
    { value: 'before', label: 'before' },
    { value: 'after', label: 'after' },
    { value: 'in_last', label: 'in last' },
    { value: 'in_next', label: 'in next' },
    { value: 'equals', label: 'equals' },
  ],
  boolean: [
    { value: 'equals', label: 'is' },
  ],
};

const Suggestions = () => {
  const [rules, setRules] = useState<SuggestionRule[]>([]);
  const [editingRule, setEditingRule] = useState<SuggestionRule | null>(null);
  const [editingConditions, setEditingConditions] = useState<Condition[]>([]);
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

  const startEditRule = (rule: SuggestionRule) => {
    setEditingRule(rule);
    try {
      const conditions = JSON.parse(rule.conditionsJson);
      setEditingConditions(conditions);
    } catch {
      setEditingConditions([]);
    }
  };

  const startNewRule = () => {
    setEditingRule({
      id: 0,
      name: '',
      description: '',
      enabled: true,
      applyToMovies: true,
      applyToSeries: true,
      conditionsJson: '[]',
      isCustom: true,
    });
    setEditingConditions([{
      field: 'lastWatched',
      operator: 'before',
      value: '180',
      valueType: 'customDays',
      logicalOperator: null,
    }]);
  };

  const cancelEdit = () => {
    setEditingRule(null);
    setEditingConditions([]);
  };

  const saveRule = async () => {
    if (!editingRule) return;

    const updatedRule = {
      ...editingRule,
      conditionsJson: JSON.stringify(editingConditions),
    };

    try {
      if (editingRule.id === 0) {
        // New rule
        await axios.post('/api/suggestion/rules', updatedRule);
      } else {
        // Update existing
        await axios.put(`/api/suggestion/rules/${editingRule.id}`, updatedRule);
      }
      
      await loadRules();
      cancelEdit();
      alert('Rule saved! Suggestions will be regenerated.');
    } catch (error) {
      console.error('Error saving rule:', error);
      alert('Error saving rule');
    }
  };

  const deleteRule = async (id: number) => {
    if (!confirm('Are you sure you want to delete this rule?')) return;

    try {
      await axios.delete(`/api/suggestion/rules/${id}`);
      await loadRules();
    } catch (error) {
      console.error('Error deleting rule:', error);
      alert('Error deleting rule');
    }
  };

  const toggleRuleEnabled = async (rule: SuggestionRule) => {
    try {
      await axios.put(`/api/suggestion/rules/${rule.id}`, {
        ...rule,
        enabled: !rule.enabled,
      });
      await loadRules();
    } catch (error) {
      console.error('Error toggling rule:', error);
    }
  };

  const addCondition = () => {
    const newCondition: Condition = {
      field: 'lastWatched',
      operator: 'before',
      value: '30',
      valueType: 'customDays',
      logicalOperator: editingConditions.length > 0 ? 'AND' : null,
    };

    // Update previous condition's logical operator
    if (editingConditions.length > 0) {
      const updated = [...editingConditions];
      updated[updated.length - 1].logicalOperator = 'AND';
      setEditingConditions([...updated, newCondition]);
    } else {
      setEditingConditions([newCondition]);
    }
  };

  const removeCondition = (index: number) => {
    const updated = editingConditions.filter((_, i) => i !== index);
    
    // Clear logical operator on last condition
    if (updated.length > 0) {
      updated[updated.length - 1].logicalOperator = null;
    }
    
    setEditingConditions(updated);
  };

  const updateCondition = (index: number, updates: Partial<Condition>) => {
    const updated = [...editingConditions];
    updated[index] = { ...updated[index], ...updates };

    // Auto-set valueType based on operator
    if (updates.operator) {
      const fieldDef = FIELDS.find(f => f.value === updated[index].field);
      if (fieldDef?.type === 'date' && ['in_last', 'in_next', 'before', 'after'].includes(updates.operator)) {
        updated[index].valueType = 'customDays';
      } else if (fieldDef?.type === 'number') {
        updated[index].valueType = 'customNumber';
      } else if (fieldDef?.type === 'boolean') {
        updated[index].valueType = 'boolean';
      } else if (fieldDef?.type === 'text') {
        updated[index].valueType = 'customText';
      }
    }

    setEditingConditions(updated);
  };

  const getFieldType = (fieldValue: string): string => {
    return FIELDS.find(f => f.value === fieldValue)?.type || 'text';
  };

  if (loading) {
    return (
      <div className="page-container">
        <div className="page-header">
          <h1>Suggestion Rules</h1>
        </div>
        <div>Loading rules...</div>
      </div>
    );
  }

  return (
    <div className="page-container">
      <div className="page-header">
        <h1>Suggestion Rules</h1>
        <button className="btn btn-primary" onClick={startNewRule}>
          <FontAwesomeIcon icon={faPlus} /> New Rule
        </button>
      </div>

      <p style={{ color: '#888', marginBottom: '30px' }}>
        Configure custom rules to automatically suggest media for deletion. Combine multiple conditions with AND/OR logic.
      </p>

      {editingRule && (
        <div className="rule-editor">
          <h2>{editingRule.id === 0 ? 'New Rule' : `Edit: ${editingRule.name}`}</h2>
          
          <div className="rule-editor-form">
            <div className="form-row">
              <label>
                Name:
                <input
                  type="text"
                  value={editingRule.name}
                  onChange={(e) => setEditingRule({ ...editingRule, name: e.target.value })}
                  placeholder="e.g., Old Unwatched Movies"
                />
              </label>
              
              <label>
                Description:
                <input
                  type="text"
                  value={editingRule.description}
                  onChange={(e) => setEditingRule({ ...editingRule, description: e.target.value })}
                  placeholder="e.g., Movies not watched in 6 months"
                />
              </label>
            </div>

            <div className="form-row">
              <label>
                <input
                  type="checkbox"
                  checked={editingRule.enabled}
                  onChange={(e) => setEditingRule({ ...editingRule, enabled: e.target.checked })}
                />
                {' '}Enabled
              </label>

              <label>
                <input
                  type="checkbox"
                  checked={editingRule.applyToMovies}
                  onChange={(e) => setEditingRule({ ...editingRule, applyToMovies: e.target.checked })}
                />
                {' '}Apply to Movies
              </label>

              <label>
                <input
                  type="checkbox"
                  checked={editingRule.applyToSeries}
                  onChange={(e) => setEditingRule({ ...editingRule, applyToSeries: e.target.checked })}
                />
                {' '}Apply to Series
              </label>
            </div>

            <div className="conditions-section">
              <h3>Conditions:</h3>
              
              {editingConditions.map((condition, index) => (
                <div key={index}>
                  <div className="condition-row">
                    <select
                      value={condition.field}
                      onChange={(e) => updateCondition(index, { field: e.target.value })}
                    >
                      {FIELDS.map(f => (
                        <option key={f.value} value={f.value}>{f.label}</option>
                      ))}
                    </select>

                    <select
                      value={condition.operator}
                      onChange={(e) => updateCondition(index, { operator: e.target.value })}
                    >
                      {OPERATORS[getFieldType(condition.field) as keyof typeof OPERATORS]?.map(op => (
                        <option key={op.value} value={op.value}>{op.label}</option>
                      ))}
                    </select>

                    {condition.valueType === 'boolean' ? (
                      <select
                        value={condition.value}
                        onChange={(e) => updateCondition(index, { value: e.target.value })}
                      >
                        <option value="true">Yes</option>
                        <option value="false">No</option>
                      </select>
                    ) : (
                      <input
                        type={condition.valueType === 'customNumber' ? 'number' : 'text'}
                        value={condition.value}
                        onChange={(e) => updateCondition(index, { value: e.target.value })}
                        placeholder={condition.valueType === 'customDays' ? 'days' : 'value'}
                      />
                    )}

                    {condition.valueType === 'customDays' && <span className="unit-label">days</span>}

                    <button
                      className="btn btn-danger btn-sm"
                      onClick={() => removeCondition(index)}
                      disabled={editingConditions.length === 1}
                    >
                      <FontAwesomeIcon icon={faTrash} />
                    </button>
                  </div>

                  {index < editingConditions.length - 1 && (
                    <div className="logical-operator">
                      <select
                        value={condition.logicalOperator || 'AND'}
                        onChange={(e) => updateCondition(index, { logicalOperator: e.target.value })}
                      >
                        <option value="AND">AND</option>
                        <option value="OR">OR</option>
                      </select>
                    </div>
                  )}
                </div>
              ))}

              {editingConditions.length < 5 && (
                <button className="btn btn-secondary" onClick={addCondition}>
                  <FontAwesomeIcon icon={faPlus} /> Add Condition
                </button>
              )}
            </div>

            <div className="rule-editor-actions">
              <button className="btn btn-primary" onClick={saveRule}>
                <FontAwesomeIcon icon={faSave} /> Save Rule
              </button>
              <button className="btn btn-secondary" onClick={cancelEdit}>
                Cancel
              </button>
            </div>
          </div>
        </div>
      )}

      <div className="rules-list">
        {rules.map(rule => (
          <div key={rule.id} className="rule-card">
            <div className="rule-header">
              <label className="rule-toggle">
                <input
                  type="checkbox"
                  checked={rule.enabled}
                  onChange={() => toggleRuleEnabled(rule)}
                />
                <span className="rule-name">{rule.name}</span>
                {!rule.isCustom && <span className="badge badge-secondary">Default</span>}
              </label>
              
              <div className="rule-actions">
                <button className="btn btn-sm btn-secondary" onClick={() => startEditRule(rule)}>
                  Edit
                </button>
                {rule.isCustom && (
                  <button className="btn btn-sm btn-danger" onClick={() => deleteRule(rule.id)}>
                    <FontAwesomeIcon icon={faTrash} />
                  </button>
                )}
              </div>
            </div>

            <div className="rule-description">
              {rule.description}
            </div>

            <div className="rule-meta">
              <span>{rule.applyToMovies ? '🎬 Movies' : ''}</span>
              <span>{rule.applyToSeries ? '📺 Series' : ''}</span>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};

export default Suggestions;
