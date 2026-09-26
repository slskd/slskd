import { getCoverUrl } from '../../lib/musicbrainz';
import React, { useEffect, useState } from 'react';

// a release's front cover, or a record placeholder when there is none
const CoverThumbnail = ({ release, size = 250 }) => {
  const [url, setUrl] = useState(undefined);
  const [missing, setMissing] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setUrl(undefined);
    setMissing(false);

    const load = async () => {
      const found = await getCoverUrl({ release, size });
      if (cancelled) return;
      setUrl(found);
      setMissing(!found);
    };

    load();

    return () => {
      cancelled = true;
    };

    // a release object is rebuilt on every render; only its identity matters
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [release.id, size]);

  if (!url) {
    return (
      <div
        className={`release-cover ${missing ? 'release-cover-missing' : 'release-cover-loading'}`}
        title={missing ? 'No cover found' : undefined}
      />
    );
  }

  return (
    <img
      alt=""
      className="release-cover"
      src={url}
    />
  );
};

export default CoverThumbnail;
