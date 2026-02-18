import './SavedFilters.css';
import {
  clearDefaultFilter,
  deleteFilter,
  getSavedFilters,
  isDefaultFilter,
  markFilterAsUsed,
  saveFilter,
  setDefaultFilter,
} from '../../../lib/savedFilters';
import React, { useEffect, useState } from 'react';
import { toast } from 'react-toastify';
import {
  Button,
  Confirm,
  Form,
  Icon,
  Input,
  Label,
  Message,
  Modal,
  Popup,
  Table,
} from 'semantic-ui-react';

const SavedFiltersActions = ({ currentFilter = '', onFilterLoad }) => {
  const [savedFilters, setSavedFilters] = useState([]);
  const [showSaveModal, setShowSaveModal] = useState(false);
  const [showManageModal, setShowManageModal] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [saveFilterName, setSaveFilterName] = useState('');
  const [selectedFilter, setSelectedFilter] = useState(null);
  const [loading, setLoading] = useState(false);

  const loadSavedFilters = () => {
    try {
      const filters = getSavedFilters();
      setSavedFilters(filters);
    } catch (loadError) {
      console.error('Error loading saved filters:', loadError);
      toast.error(loadError.message);
    }
  };

  useEffect(() => {
    loadSavedFilters();
  }, []);

  const handleSaveFilter = async () => {
    if (!saveFilterName.trim()) {
      toast.error('Please enter a name for the filter');
      return;
    }

    if (!currentFilter.trim()) {
      toast.error('No filter to save');
      return;
    }

    try {
      setLoading(true);

      const success = saveFilter(saveFilterName.trim(), currentFilter);
      if (success) {
        toast.success(`Filter "${saveFilterName}" saved successfully`);
        setSaveFilterName('');
        setShowSaveModal(false);
        loadSavedFilters();
      }
    } catch (saveError) {
      console.error('Error saving filter:', saveError);
      toast.error(saveError.message);
    } finally {
      setLoading(false);
    }
  };

  const handleLoadFilter = (filterName) => {
    try {
      const filters = getSavedFilters();
      const filter = filters.find((f) => f.name === filterName);

      if (!filter) {
        throw new Error(`Filter "${filterName}" not found`);
      }

      onFilterLoad(filter.filterString);
      markFilterAsUsed(filterName);
      toast.success(`Loaded filter: ${filterName}`);
    } catch (loadError) {
      console.error('Error loading filter:', loadError);
      toast.error(loadError.message);
      // Refresh the filters list in case the filter was deleted externally
      loadSavedFilters();
    }
  };

  const handleDeleteFilter = async () => {
    if (!selectedFilter) return;

    try {
      setLoading(true);

      const success = deleteFilter(selectedFilter.name);
      if (success) {
        toast.success(`Filter "${selectedFilter.name}" deleted`);
        loadSavedFilters();
        setSelectedFilter(null);
      } else {
        throw new Error('Delete operation returned false');
      }
    } catch (deleteError) {
      console.error('Error deleting filter:', deleteError);
      toast.error(deleteError.message);
    } finally {
      setLoading(false);
      setShowDeleteConfirm(false);
    }
  };

  const handleSetDefaultFilter = (filterName) => {
    try {
      const success = setDefaultFilter(filterName);
      if (success) {
        toast.success(`"${filterName}" set as default filter`);
        loadSavedFilters();
      } else {
        throw new Error('Set default operation returned false');
      }
    } catch (setDefaultError) {
      console.error('Error setting default filter:', setDefaultError);
      toast.error(setDefaultError.message);
    }
  };

  const handleClearDefaultFilter = () => {
    try {
      const success = clearDefaultFilter();
      if (success) {
        toast.success('Default filter cleared');
        loadSavedFilters();
      } else {
        throw new Error('Clear default operation returned false');
      }
    } catch (clearDefaultError) {
      console.error('Error clearing default filter:', clearDefaultError);
      toast.error(clearDefaultError.message);
    }
  };

  const openDeleteConfirm = (filter) => {
    setSelectedFilter(filter);
    setShowDeleteConfirm(true);
  };

  return (
    <>
      <Popup
        content="Save current filter"
        trigger={
          <Button
            disabled={!currentFilter.trim()}
            icon="save"
            onClick={() => setShowSaveModal(true)}
            size="small"
          />
        }
      />

      <Popup
        content="Load filter"
        trigger={
          <Button
            icon="folder open"
            onClick={() => setShowManageModal(true)}
            size="small"
          />
        }
      />

      <Modal
        onClose={() => setShowSaveModal(false)}
        open={showSaveModal}
        size="small"
      >
        <Modal.Header>Save Search Filter</Modal.Header>
        <Modal.Content>
          <Message info>
            <Message.Header>
              <Icon name="info circle" />
              Browser Storage
            </Message.Header>
            <p>
              Saved filters and default settings are stored locally in your
              browser. They will persist across sessions but are not synced
              between devices or browsers.
            </p>
          </Message>
          <Form>
            <Form.Field id="saveFilterName">
              <label htmlFor="saveFilterName">Filter Name</label>
              <Input
                autoFocus
                onChange={(_, { value }) => setSaveFilterName(value)}
                onKeyPress={(event) =>
                  event.key === 'Enter' && handleSaveFilter()
                }
                placeholder="Enter a name for this filter"
                value={saveFilterName}
              />
            </Form.Field>
            <Form.Field id="filterString">
              <label htmlFor="filterString">Filter String</label>
              <Input
                placeholder="No filter to save"
                readOnly
                value={currentFilter}
              />
            </Form.Field>
          </Form>
        </Modal.Content>
        <Modal.Actions>
          <Button
            onClick={() => {
              setShowSaveModal(false);
            }}
          >
            Cancel
          </Button>
          <Button
            disabled={
              !saveFilterName.trim() || !currentFilter.trim() || loading
            }
            loading={loading}
            onClick={handleSaveFilter}
            primary
          >
            Save Filter
          </Button>
        </Modal.Actions>
      </Modal>

      <Modal
        onClose={() => setShowManageModal(false)}
        open={showManageModal}
        size="large"
      >
        <Modal.Header>
          <Icon name="folder open" />
          Manage Saved Filters
        </Modal.Header>
        <Modal.Content>
          {savedFilters.length === 0 ? (
            <Message info>
              <Message.Header>No saved filters</Message.Header>
              <p>You haven't saved any search filters yet.</p>
            </Message>
          ) : (
            <Table
              className="unstackable saved-filters-table"
              size="small"
            >
              <Table.Header>
                <Table.Row>
                  <Table.HeaderCell>Name</Table.HeaderCell>
                  <Table.HeaderCell>Filter</Table.HeaderCell>
                  <Table.HeaderCell>Created</Table.HeaderCell>
                  <Table.HeaderCell>Last Used</Table.HeaderCell>
                  <Table.HeaderCell>Actions</Table.HeaderCell>
                </Table.Row>
              </Table.Header>
              <Table.Body>
                {savedFilters
                  .sort((a, b) => a.name.localeCompare(b.name))
                  .map((filter) => (
                    <Table.Row key={filter.name}>
                      <Table.Cell className="filter-name">
                        <div className="filter-name-container">
                          <strong>{filter.name}</strong>
                          {isDefaultFilter(filter.name) && (
                            <Label
                              color="yellow"
                              horizontal
                              size="mini"
                            >
                              Default
                            </Label>
                          )}
                        </div>
                      </Table.Cell>
                      <Table.Cell>
                        <code className="filter-string-inline">
                          {filter.filterString}
                        </code>
                      </Table.Cell>
                      <Table.Cell>
                        {new Date(filter.createdAt).toLocaleDateString()}
                      </Table.Cell>
                      <Table.Cell>
                        {filter.lastUsed
                          ? new Date(filter.lastUsed).toLocaleDateString()
                          : '-'}
                      </Table.Cell>
                      <Table.Cell>
                        <div className="filter-actions">
                          <Button
                            disabled={loading}
                            icon="play"
                            onClick={() => {
                              handleLoadFilter(filter.name);
                              setShowManageModal(false);
                            }}
                            size="mini"
                            title="Load this filter"
                          />
                          {isDefaultFilter(filter.name) ? (
                            <Button
                              color="yellow"
                              disabled={loading}
                              icon="star outline"
                              onClick={handleClearDefaultFilter}
                              size="mini"
                              title="Remove as default filter"
                            />
                          ) : (
                            <Button
                              disabled={loading}
                              icon="star"
                              onClick={() =>
                                handleSetDefaultFilter(filter.name)
                              }
                              size="mini"
                              title="Set as default filter"
                            />
                          )}
                          <Button
                            color="red"
                            disabled={loading}
                            icon="trash"
                            onClick={() => openDeleteConfirm(filter)}
                            size="mini"
                            title="Delete this filter"
                          />
                        </div>
                      </Table.Cell>
                    </Table.Row>
                  ))}
              </Table.Body>
            </Table>
          )}
        </Modal.Content>
        <Modal.Actions>
          <Button
            onClick={() => {
              setShowManageModal(false);
            }}
          >
            Close
          </Button>
        </Modal.Actions>
      </Modal>

      {/* Delete Confirmation */}
      <Confirm
        cancelButton="Cancel"
        confirmButton="Delete"
        content={`Are you sure you want to delete the filter "${selectedFilter?.name}"?`}
        header="Delete Filter"
        onCancel={() => setShowDeleteConfirm(false)}
        onConfirm={handleDeleteFilter}
        open={showDeleteConfirm}
      />
    </>
  );
};

export default SavedFiltersActions;
