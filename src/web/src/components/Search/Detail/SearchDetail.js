import React, { useEffect, useState, useMemo } from 'react';

import {
  Loader,
  Checkbox,
  Input,
  Segment,
  Dropdown,
  Button,
} from 'semantic-ui-react';

import ErrorSegment from '../../Shared/ErrorSegment';
import Response from '../Response';
import { getResponses, parseFiltersFromString, filterResponse } from '../../../lib/searches';
import LoaderSegment from '../../Shared/LoaderSegment';

const sortDropdownOptions = [
  { key: 'uploadSpeed', text: 'Upload Speed (Fastest to Slowest)', value: 'uploadSpeed' },
  { key: 'queueLength', text: 'Queue Depth (Least to Most)', value: 'queueLength' }
];

const SearchDetail = ({ search, onStop, onBack }) => {
  const { id, state, isComplete, fileCount, lockedFileCount, responseCount } = search;

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(undefined);

  const [results, setResults] = useState([]);

  const [hiddenResults, setHiddenResults] = useState([]);
  const [resultSort, setResultSort] = useState('uploadSpeed');
  const [hideLocked, setHideLocked] = useState(true);
  const [hideNoFreeSlots, setHideNoFreeSlots] = useState(false);
  const [foldResults, setFoldResults] = useState(false);
  const [resultFilters, setResultFilters] = useState('');
  const [displayCount, setDisplayCount] = useState(5);

  useEffect(() => {
    const get = async () => {
      try {
        setLoading(true);
        const responses = await getResponses({ id });
        setResults(responses);
        setLoading(false);
      } catch (error) {
        setError(error);
        setLoading(false);
      }
    }

    if (isComplete) {
      get();
    }
  }, [id, isComplete])

  const sortedAndFilteredResults = useMemo(() => {
    const sortOptions = {
      uploadSpeed: { field: 'uploadSpeed', order: 'desc' },
      queueLength: { field: 'queueLength', order: 'asc' }
    }

    const { field, order } = sortOptions[resultSort];

    const filters = parseFiltersFromString(resultFilters);

    return results
      .filter(r => !hiddenResults.includes(r.username))
      .map(r => {
        if (hideLocked) {
          return { ...r, lockedFileCount: 0, lockedFiles: [] }
        }
        return r;
      })
      .map(response => filterResponse({ response, filters }))
      .filter(r => r.fileCount + r.lockedFileCount > 0)
      .filter(r => !(hideNoFreeSlots && r.freeUploadSlots === 0))
      .sort((a, b) => {
        if (order === 'asc') {
          return a[field] - b[field];
        }

        return b[field] - a[field];
      });

  }, [results, hideLocked, hideNoFreeSlots, resultFilters, resultSort, hiddenResults])

  const filteredCount = results?.length - sortedAndFilteredResults.length;
  const remainingCount = sortedAndFilteredResults.length - displayCount;

  if (error) {
    return (<ErrorSegment caption={error?.message ?? error}/>)
  }

  if (!isComplete) {
    return (
      <Loader className='search-loader' active inline='centered' size='big'>
        {state === 'InProgress' ? <span>Found {fileCount} files {lockedFileCount > 0 ? `(plus ${lockedFileCount} locked) ` : ''}from {responseCount} users</span>
        : 'Loading results...'}
      </Loader>
    );
  }

  if (loading) {
    return (<LoaderSegment/>)
  }

  return (
    <>
        <Segment className='search-segment' raised>
          {/* <Input
            input={<input placeholder="Search phrase" type="search" data-lpignore="true"></input>}
            size='big'
            ref={searchRef}
            disabled={true}
            className='search-input'
            placeholder="Search phrase"
            action={<Button icon='x' color='red' onClick={() => history.push(`/searches`)}/>}
          /> */}
          <Button
            negative
            icon={isComplete ? 'arrow left' : 'stop circle'}
            onClick={() => {
              if (isComplete) {
                onBack();
              } else {
                onStop(search);
              }
            }}
          />
        </Segment>
        {(results && results.length > 0) && 
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
              placeholder='lackluster container -bothersome iscbr|isvbr islossless|islossy minbitrate:320 minfilesize:10 minfilesinfolder:8 minlength:5000'
              label={{ icon: 'filter', content: 'Filter' }}
              value={resultFilters}
              onChange={(e, data) => setResultFilters(data.value)}
              action={!!resultFilters && { icon: 'x', color: 'red', onClick: () => setResultFilters('')}}
            />
          </Segment>
        }
        {sortedAndFilteredResults.slice(0, displayCount).map((r, i) =>
          <Response
            key={i}
            response={r}
            onHide={() => setHiddenResults([...hiddenResults, r.username])}
            isInitiallyFolded={foldResults}
          />
        )}
        {remainingCount > 0 ?
          <Button className='showmore-button' size='large' fluid primary
            onClick={() => setDisplayCount(displayCount + 5)}>
              Show {remainingCount > 5 ? 5 : remainingCount} More Results {`(${remainingCount} remaining, ${filteredCount} hidden by filter(s))`}
          </Button>
          : filteredCount > 0 ? 
            <Button className='showmore-button' size='large' fluid disabled>{`All results shown. ${filteredCount} results hidden by filter(s)`}</Button> : ''}
      </>
  )
}

export default SearchDetail;