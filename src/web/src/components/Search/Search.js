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
    searchId: '',
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

        this.setState({ searchPhrase, searchId, searchState: 'pending' }, () => {
            search.search({ id: searchId, searchText: searchPhrase })
            .then(response => this.setState({ results: response.data }))
            .then(() => this.setState({ searchState: 'complete' }, () => {
                this.saveState();
                this.setSearchText();
            }))
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

    saveState = () => {
        try {
            localStorage.setItem('soulseek-example-search-state', JSON.stringify(this.state));
        } catch(error) {
            console.log(error);
        }
    }

    loadState = () => {
        this.setState(JSON.parse(localStorage.getItem('soulseek-example-search-state')) || initialState);
    }

    componentDidMount = () => {
        this.fetchStatus();
        this.loadState();
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

    fetchStatus = () => {
        if (this.state.searchState === 'pending') {
            search.getStatus({ id: this.state.searchId })
            .then(response => this.setState({
                searchStatus: response
            }));
        }
    }

    showMore = () => {
        this.setState({ displayCount: this.state.displayCount + 5 }, () => this.saveState());
    }

    sortAndFilterResults = () => {
        const { results, hideNoFreeSlots, resultSort, hideLocked, hiddenResults } = this.state;
        const { field, order } = sortOptions[resultSort];

        return results
            .filter(r => !hiddenResults.includes(r.username))
            .map(r => {
                if (hideLocked) {
                    return { ...r, lockedFileCount: 0, lockedFiles: [] }
                }
                return r;
            })
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
        let { searchState, searchStatus, results, displayCount, resultSort, hideNoFreeSlots, hideLocked, hiddenResults } = this.state;
        let pending = searchState === 'pending';

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
                        Found {searchStatus.fileCount} files {searchStatus.lockedFileCount > 0 ? `(plus ${searchStatus.lockedFileCount} locked) ` : ''}from {searchStatus.responseCount} users
                    </Loader>
                :
                    <div>
                        {(results && results.length > 0) ? <Segment className='search-options' raised>
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
                        </Segment> : <PlaceholderSegment icon='search'/>}
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