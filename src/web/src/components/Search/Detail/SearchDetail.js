import React, { useEffect, useState } from 'react';

import { getResponses } from '../../../lib/searches';

const SearchDetail = ({ search }) => {
  const [responses, setResponses] = useState([]);

  useEffect(() => {
    const get = async () => {
      console.log('fetching responses')
      const responses = await getResponses({ id: search.id });
      console.log('responses', responses)
      setResponses(responses);
    }

    get();
  }, [search.id])

  return (
    <>
      <span>{JSON.stringify(search)}</span>
      {responses.map(r => <li>{JSON.stringify(r)}</li>)}
    </>
  )
}

export default SearchDetail;