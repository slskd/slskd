import React, { useEffect, useState, useMemo } from 'react';

import {
  Checkbox,
  Input,
  Segment,
  Dropdown,
  Button,
} from 'semantic-ui-react';

import { sleep } from '../../../lib/util';
import Switch from '../../Shared/Switch';
import ErrorSegment from '../../Shared/ErrorSegment';
import { Response } from '../Response';
import { getResponses, parseFiltersFromString, filterResponse } from '../../../lib/searches';
import LoaderSegment from '../../Shared/LoaderSegment';
import SearchDetailHeader from './SearchDetailHeader';

const sortDropdownOptions = [
  { key: 'uploadSpeed', text: 'Upload Speed (Fastest to Slowest)', value: 'uploadSpeed' },
  { key: 'queueLength', text: 'Queue Depth (Least to Most)', value: 'queueLength' },
];

const SearchDetail = ({ search, creating, stopping, removing, disabled, onCreate, onStop, onRemove }) => {
  const { id, state, isComplete, fileCount, lockedFileCount, responseCount } = search;

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(undefined);

  const [results, setResults] = useState([]);

  // filters and sorting options
  const [hiddenResults, setHiddenResults] = useState([]);
  const [resultSort, setResultSort] = useState('uploadSpeed');
  const [hideLocked, setHideLocked] = useState(true);
  const [hideNoFreeSlots, setHideNoFreeSlots] = useState(false);
  const [foldResults, setFoldResults] = useState(false);
  const [resultFilters, setResultFilters] = useState('');
  const [displayCount, setDisplayCount] = useState(5);

  // when the search transitions from !isComplete -> isComplete,
  // fetch the results from the server
  useEffect(() => {
    const get = async () => {
      try {
        setLoading(true);

        // the results may not be ready yet.  this is very rare, but
        // if it happens the search will complete with no results.
        await sleep(500);

        const responses = await getResponses({ id });
        setResults(responses);
        setLoading(false);
      } catch (error) {
        setError(error);
        setLoading(false);
      }
    };

    if (isComplete) {
      get();
    }
  }, [id, isComplete]);

  // apply sorting and filters.  this can take a while for larger result
  // sets, so memoize it.
  const sortedAndFilteredResults = useMemo(() => {
    const sortOptions = {
      uploadSpeed: { field: 'uploadSpeed', order: 'desc' },
      queueLength: { field: 'queueLength', order: 'asc' },
    };

    const { field, order } = sortOptions[resultSort];

    const filters = parseFiltersFromString(resultFilters);

    return results
      .filter(r => !hiddenResults.includes(r.username))
      .map(r => {
        if (hideLocked) {
          return { ...r, lockedFileCount: 0, lockedFiles: [] };
        }
        return r;
      })
      .map(response => filterResponse({ response, filters }))
      .filter(r => r.fileCount + r.lockedFileCount > 0)
      .filter(r => !(hideNoFreeSlots && !r.hasFreeUploadSlot))
      .sort((a, b) => {
        if (order === 'asc') {
          return a[field] - b[field];
        }

        return b[field] - a[field];
      });

  }, [results, hideLocked, hideNoFreeSlots, resultFilters, resultSort, hiddenResults]);

  // when a user uses the action buttons, we will *probably* re-use this component,
  // but with a new search ID.  clear everything to prepare for the transition
  const reset = () => {
    setLoading(false);
    setError(undefined);
    setResults([]);
    setHiddenResults([]);
    setDisplayCount(5);
  };

  const create = async ({ search, navigate }) => {
    reset();
    onCreate({ search, navigate });
  };

  const remove = async () => {
    reset();
    onRemove(search);
  };

  const filteredCount = results?.length - sortedAndFilteredResults.length;
  const remainingCount = sortedAndFilteredResults.length - displayCount;
  const loaded = (!removing && !creating && !loading && results && results.length > 0);

  if (error) {
    return (<ErrorSegment caption={error?.message ?? error} />);
  }

  return (
    <>
      <SearchDetailHeader
        loading={loading}
        loaded={loaded}
        removing={removing}
        stopping={stopping}
        creating={creating}
        disabled={disabled}
        search={search}
        onCreate={create}
        onStop={onStop}
        onRemove={remove}
      />
      <Switch
        searching={!isComplete &&
          <LoaderSegment>
            {state === 'InProgress'
              ? `Found ${fileCount} files ${lockedFileCount > 0
                ? `(plus ${lockedFileCount} locked) `
                : ''}from ${responseCount} users`
              : 'Loading results...'}
          </LoaderSegment>
        }
        loading={loading && <LoaderSegment />}
      >
        {loaded &&
          <Segment className='search-options' raised>
            <Dropdown
              button
              className='search-options-sort icon'
              floating
              labeled
              icon='sort'
              options={sortDropdownOptions}
              onChange={(e, { value }) => setResultSort(value)}
              text={sortDropdownOptions.find(o => o.value === resultSort).text}
            />
            <div className='search-option-toggles'>
              <Checkbox
                className='search-options-hide-locked'
                toggle
                onChange={() => setHideLocked(!hideLocked)}
                checked={hideLocked}
                label='Hide Locked Results'
              />
              <Checkbox
                className='search-options-hide-no-slots'
                toggle
                onChange={() => setHideNoFreeSlots(!hideNoFreeSlots)}
                checked={hideNoFreeSlots}
                label='Hide Results with No Free Slots'
              />
              <Checkbox
                className='search-options-fold-results'
                toggle
                onChange={() => setFoldResults(!foldResults)}
                checked={foldResults}
                label='Fold Results'
              />
            </div>
            <Input
              className='search-filter'
              placeholder='
                lackluster container -bothersome iscbr|isvbr islossless|islossy 
                minbitrate:320 minbitdepth:24 minfilesize:10 minfilesinfolder:8 minlength:5000
              '
              label={{ icon: 'filter', content: 'Filter' }}
              value={resultFilters}
              onChange={(e, data) => setResultFilters(data.value)}
              action={!!resultFilters && { icon: 'x', color: 'red', onClick: () => setResultFilters('') }}
            />
          </Segment>
        }
        {loaded && sortedAndFilteredResults.slice(0, displayCount).map((r, i) =>
          <Response
            key={i}
            response={r}
            disabled={disabled}
            onHide={() => setHiddenResults([...hiddenResults, r.username])}
            isInitiallyFolded={foldResults}
          />
        )}
        {loaded && (remainingCount > 0 ?
          <Button className='showmore-button' size='large' fluid primary
            onClick={() => setDisplayCount(displayCount + 5)}>
            Show {remainingCount > 5
              ? 5
              : remainingCount} More Results {`(${remainingCount} remaining, ${filteredCount} hidden by filter(s))`}
          </Button>
          : filteredCount > 0 ?
            <Button className='showmore-button' size='large' fluid disabled>{
              `All results shown. ${filteredCount} results hidden by filter(s)`
            }</Button> : '')}
      </Switch>
    </>
  );
};

export default SearchDetail;