import '../Search/Search.css';
import { urlBase } from '../../config';
import * as library from '../../lib/searches';
import React, { useEffect, useRef, useState } from 'react';
import { Link, useHistory } from 'react-router-dom';
import { toast } from 'react-toastify';
import { Button, Icon, Input, Segment } from 'semantic-ui-react';
import { v4 as uuidv4 } from 'uuid';

const SearchBar = ({ server } = {}) => {
  const [creating, setCreating] = useState(false);

  const inputRef = useRef();
  const history = useHistory();

  const create = async ({ navigate = false } = {}) => {
    const ref = inputRef?.current?.inputRef?.current;
    const searchText = ref.value;
    const id = uuidv4();

    try {
      setCreating(true);
      await library.create({ id, searchText });

      ref.value = '';
      ref.focus();

      setCreating(false);

      if (navigate) {
        history.push(`${urlBase}/searches/${id}`);
      } else {
        const label =
          searchText.length > 30 ? `${searchText.slice(0, 15)}...` : searchText;
        toast.info(
          <span>
            Search for &lsquo;{label}&rsquo; started.{' '}
            <Link to={`${urlBase}/searches/${id}`}>View results</Link>
          </span>,
        );
      }
    } catch (createError) {
      console.error(createError);
      toast.error(
        createError?.response?.data ?? createError?.message ?? createError,
      );
      setCreating(false);
    }
  };

  useEffect(() => {
    inputRef?.current?.inputRef?.current?.focus();
  }, []);

  return (
    <Segment
      className="search-segment"
      raised
    >
      <div className="search-segment-icon">
        <Icon
          name="search"
          size="big"
        />
      </div>
      <Input
        action={
          <>
            <Button
              disabled={creating || !server?.isConnected}
              icon="plus"
              onClick={create}
            />
            <Button
              disabled={creating || !server?.isConnected}
              icon="search"
              onClick={() => create({ navigate: true })}
            />
          </>
        }
        className="search-input"
        disabled={creating || !server?.isConnected}
        input={
          <input
            data-lpignore="true"
            placeholder={
              server?.isConnected
                ? 'Search phrase'
                : 'Connect to server to perform a search'
            }
            type="search"
          />
        }
        loading={creating}
        onKeyUp={(keyUpEvent) => (keyUpEvent.key === 'Enter' ? create() : '')}
        placeholder="Search phrase"
        ref={inputRef}
        size="big"
      />
    </Segment>
  );
};

export default SearchBar;
