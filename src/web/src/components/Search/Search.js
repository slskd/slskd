import { v4 as uuidv4 } from 'uuid';
import React, { Component } from 'react';
import * as search from '../../lib/search';

import './Search.css';

import Response from './Response';
import PlaceholderSegment from '../Shared/PlaceholderSegment';

import {
  Segment,
  Input,
  Loader,
  Button,
  Dropdown,
  Checkbox
} from 'semantic-ui-react';

const initialState = {
  searchPhrase: '',
  searchId: undefined,
  searchState: 'idle',
  searchStatus: {
    responseCount: 0,
    fileCount: 0
  },
  results: [],
  interval: undefined,
  displayCount: 5,
  resultSort: 'uploadSpeed',
  hideNoFreeSlots: true,
  hiddenResults: [],
  hideLocked: true,
  fetching: false,
  resultFilters: ''
};

const sortOptions = {
  uploadSpeed: { field: 'uploadSpeed', order: 'desc' },
  queueLength: { field: 'queueLength', order: 'asc' }
}

const sortDropdownOptions = [
  { key: 'uploadSpeed', text: 'Upload Speed (Fastest to Slowest)', value: 'uploadSpeed' },
  { key: 'queueLength', text: 'Queue Depth (Least to Most)', value: 'queueLength' }
];

class Search extends Component {
  state = initialState;

  search = () => {
    const searchPhrase = this.inputtext.inputRef.current.value;
    const searchId = uuidv4();

    this.setState({ searchPhrase, searchId, searchState: 'pending' }, async () => {
      this.saveState();
      search.search({ id: searchId, searchText: searchPhrase });
    });
  }

  clear = () => {
    this.setState(initialState, () => {
      this.saveState();
      this.setSearchText();
      this.inputtext.focus();
    });
  }

  keyUp = (event) => event.key === 'Escape' ? this.clear() : '';

  onSearchPhraseChange = (event, data) => {
    this.setState({ searchPhrase: data.value });
  }

  onResultFilterChange = (event, data) => {
    this.setState({ resultFilters: data.value }, () => this.saveState());
  }

  clearResultFilter = () => {
    this.setState({ resultFilters: '' }, () => this.saveState());
  }

  saveState = () => {
    try {
      localStorage.setItem('soulseek-example-search-state', JSON.stringify({ ...this.state, results: [] }));
    } catch(error) {
      console.log(error);
    }
  }

  loadState = () => {
    return new Promise((resolve) => 
      this.setState(JSON.parse(localStorage.getItem('soulseek-example-search-state')) || initialState, resolve));
  }

  componentDidMount = async () => {
    await this.loadState();
    this.fetchResults();
    this.setState({
      interval: window.setInterval(this.fetchStatus, 500)
    }, () => this.setSearchText());

    document.addEventListener("keyup", this.keyUp, false);
  }

  setSearchText = () => {
    this.inputtext.inputRef.current.value = this.state.searchPhrase;
    this.inputtext.inputRef.current.disabled = this.state.searchState !== 'idle';
  }

  componentWillUnmount = () => {
    clearInterval(this.state.interval);
    this.setState({ interval: undefined });
    document.removeEventListener("keyup", this.keyUp, false);
  }

  fetchResults = async () => {
    const { searchId } = this.state;

    if (!!searchId) {
      this.setState({ fetching: true }, async () => {
        try {
          const responses = await search.getResponses({ id: searchId });

          this.setState({
            results: responses,
            fetching: false
          }, this.saveState);
        } catch (error) {
          console.log(error);
          this.clear();
        }
      });
    }
  }

  fetchStatus = async () => {
    const { searchState, searchId } = this.state;

    if (searchState === 'pending') {
      this.setState({ fetching: true }, async () => {
        const response = await search.getStatus({ id: searchId });

        if (response.isComplete) {
          this.setState({
            searchState: 'complete'
          }, this.fetchResults);
        } else {
          this.setState({
            searchStatus: response,
            fetching: false
          }, this.saveState);
        }
      });
    }
  }

  showMore = () => {
    this.setState({ displayCount: this.state.displayCount + 5 }, () => this.saveState());
  }

  sortAndFilterResults = () => {
    const { results = [], hideNoFreeSlots, resultSort, resultFilters = '', hideLocked, hiddenResults = [] } = this.state;
    const { field, order } = sortOptions[resultSort];

    const filters = search.parseFiltersFromString(resultFilters);

    return results
      .filter(r => !hiddenResults.includes(r.username))
      .map(r => {
        if (hideLocked) {
          return { ...r, lockedFileCount: 0, lockedFiles: [] }
        }
        return r;
      })
      .map(response => search.filterResponse({ response, filters }))
      .filter(r => r.fileCount + r.lockedFileCount > 0)
      .filter(r => !(hideNoFreeSlots && r.freeUploadSlots === 0))
      .sort((a, b) => {
        if (order === 'asc') {
          return a[field] - b[field];
        }

        return b[field] - a[field];
      });
  }

  hideResult = (result) => {
    this.setState({ hiddenResults: [...this.state.hiddenResults, result.username]}, () => {
      if (this.state.hiddenResults.length === this.state.results.length) {
        this.clear();
      } else {
        this.saveState();
      }
    });
  }

  render = () => {
    let { searchState, searchStatus, results = [], displayCount, resultSort, hideNoFreeSlots, hideLocked, hiddenResults = [], fetching, resultFilters } = this.state;
    let pending = fetching || searchState === 'pending';

    const sortedAndFilteredResults = this.sortAndFilterResults();

    const remainingCount = sortedAndFilteredResults.length - displayCount;
    const showMoreCount = remainingCount >= 5 ? 5 : remainingCount;
    const hiddenCount = results.length - hiddenResults.length - sortedAndFilteredResults.length;

    return (
      <div className='search-container'>
        <Segment className='search-segment' raised>
          <Input
            input={<input placeholder="Search phrase" type="search" data-lpignore="true"></input>}
            size='big'
            ref={input => this.inputtext = input}
            loading={pending}
            disabled={pending}
            className='search-input'
            placeholder="Search phrase"
            action={!pending && (searchState === 'idle' ? { icon: 'search', onClick: this.search } : { icon: 'x', color: 'red', onClick: this.clear })}
            onKeyUp={(e) => e.key === 'Enter' ? this.search() : ''}
          />
        </Segment>
        {pending ?
          <Loader
            className='search-loader'
            active
            inline='centered'
            size='big'
          >
            {searchState === 'pending' ? <span>Found {searchStatus.fileCount} files {searchStatus.lockedFileCount > 0 ? `(plus ${searchStatus.lockedFileCount} locked) ` : ''}from {searchStatus.responseCount} users</span>
            : 'Loading results...'}
          </Loader>
        :
          <div>
            {(results && results.length > 0) ? 
              <Segment className='search-options' raised>
                <Dropdown
                  button
                  className='search-options-sort icon'
                  floating
                  labeled
                  icon='sort'
                  options={sortDropdownOptions}
                  onChange={(e, { value }) => this.setState({ resultSort: value }, () => this.saveState())}
                  text={sortDropdownOptions.find(o => o.value === resultSort).text}
                />
                <div className='search-option-toggles'>
                  <Checkbox
                    className='search-options-hide-locked'
                    toggle
                    onChange={() => this.setState({ hideLocked: !hideLocked }, () => this.saveState())}
                    checked={hideLocked}
                    label='Hide Locked Results'
                  />
                  <Checkbox
                    className='search-options-hide-no-slots'
                    toggle
                    onChange={() => this.setState({ hideNoFreeSlots: !hideNoFreeSlots }, () => this.saveState())}
                    checked={hideNoFreeSlots}
                    label='Hide Results with No Free Slots'
                  />
                </div>
                <Input 
                  className='search-filter'
                  placeholder='lackluster container -bothersome iscbr|isvbr islossless minbitrate:320 minfilesize:10 minfilesinfolder:8 minlength:5000'
                  label={{ icon: 'filter', content: 'Filter' }}
                  value={resultFilters}
                  onChange={this.onResultFilterChange}
                  action={!!resultFilters && { icon: 'x', color: 'red', onClick: this.clearResultFilter }}
                />
              </Segment> : <PlaceholderSegment icon='search'/>
            }
            {sortedAndFilteredResults.slice(0, displayCount).map((r, i) =>
              <Response
                key={i}
                response={r}
                onDownload={this.props.onDownload}
                onHide={() => this.hideResult(r)}
              />
            )}
            {remainingCount > 0 ?
              <Button
                className='showmore-button'
                size='large'
                fluid
                primary
                onClick={() => this.showMore()}>
                  Show {showMoreCount} More Results {remainingCount > 5 ? `(${remainingCount} remaining, ${hiddenCount} hidden by filter(s))` : ''}
              </Button>
              : ''}
          </div>}
      </div>
    )
  }
}

export default Search;