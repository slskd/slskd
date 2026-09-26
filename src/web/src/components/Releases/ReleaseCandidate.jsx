import { formatDuration } from '../../lib/musicbrainz';
import { downloadPlan } from '../../lib/releases';
import { enqueueBatch } from '../../lib/transfers';
import { formatBytes, formatSpeed, getFileName } from '../../lib/util';
import UserLink from '../Shared/UserLink';
import React, { useState } from 'react';
import { toast } from 'react-toastify';
import {
  Button,
  Checkbox,
  Icon,
  Label,
  Segment,
  Table,
} from 'semantic-ui-react';

const errorText = (error) =>
  error?.response?.data?.message ??
  error?.response?.data ??
  error?.message ??
  String(error);

// how far a file's length is from the track's, in seconds
const LengthDelta = ({ file, track }) => {
  if (!file?.length || !track.length) return null;

  const delta = Math.round(file.length - track.length / 1_000);
  if (Math.abs(delta) <= 1) return null;

  return (
    <span
      className={`release-length-delta ${Math.abs(delta) > 8 ? 'far' : ''}`}
      title="Difference from the MusicBrainz length"
    >
      {delta > 0 ? '+' : ''}
      {delta}s
    </span>
  );
};

const fileQuality = (file) => {
  if (file.bitDepth && file.sampleRate) {
    return `${file.bitDepth}/${Number((file.sampleRate / 1_000).toFixed(1))}`;
  }

  return file.bitRate ? `${file.bitRate} kbps` : '';
};

const CandidateSummary = ({ candidate }) => {
  const { complete, extras, matchedCount, quality, trackCount, user } =
    candidate;

  return (
    <>
      <div className="release-candidate-header">
        <Label
          className="release-candidate-completeness"
          color={complete ? 'green' : 'yellow'}
          title={`${matchedCount} of ${trackCount} tracks found in this folder`}
        >
          <Icon name={complete ? 'check' : 'adjust'} />
          {matchedCount}/{trackCount}
        </Label>
        {quality.label && (
          <Label className="release-candidate-quality">{quality.label}</Label>
        )}
        <span className="release-candidate-user">
          <Icon
            color={user.hasFreeUploadSlot ? 'green' : 'yellow'}
            name="circle"
            title={
              user.hasFreeUploadSlot
                ? 'Free upload slot'
                : 'No free upload slot'
            }
          />
          <UserLink username={user.username} />
        </span>
        <span className="release-candidate-meta">
          {formatBytes(candidate.size)} · {formatSpeed(user.uploadSpeed)} ·
          queue {user.queueLength ?? '?'}
        </span>
      </div>
      <div
        className="release-candidate-folder"
        title={candidate.folder}
      >
        {candidate.folder}
      </div>
      {(extras.length > 0 || candidate.locked) && (
        <div className="release-candidate-notes">
          {extras.length > 0 && (
            <span>
              <Icon name="plus circle" />
              {extras.length} audio file{extras.length === 1 ? '' : 's'} not on
              this release
            </span>
          )}
          {candidate.locked && (
            <span>
              <Icon name="lock" />
              some files are locked
            </span>
          )}
        </div>
      )}
    </>
  );
};

const CandidateTracks = ({ extras, matches }) => (
  <Table
    className="release-candidate-tracks"
    compact
    unstackable
    very
  >
    <Table.Body>
      {matches.map(({ file, track }) => (
        <Table.Row
          key={`${track.disc}-${track.position}`}
          negative={!file}
        >
          <Table.Cell className="release-tracklist-number">
            {track.number}
          </Table.Cell>
          <Table.Cell>{track.title}</Table.Cell>
          <Table.Cell className="release-tracklist-length">
            {formatDuration(track.length)}
          </Table.Cell>
          <Table.Cell className="release-candidate-file">
            {file ? (
              <>
                {file.locked && <Icon name="lock" />}
                {getFileName(file.filename)}
              </>
            ) : (
              <span className="release-candidate-missing">missing</span>
            )}
          </Table.Cell>
          <Table.Cell className="release-candidate-file-quality">
            {file && fileQuality(file)}{' '}
            <LengthDelta
              file={file}
              track={track}
            />
          </Table.Cell>
        </Table.Row>
      ))}
      {extras.map((file) => (
        <Table.Row
          className="release-candidate-extra"
          key={file.filename}
        >
          <Table.Cell />
          <Table.Cell colSpan={2}>
            <span className="release-candidate-missing">
              not on this release
            </span>
          </Table.Cell>
          <Table.Cell className="release-candidate-file">
            {getFileName(file.filename)}
          </Table.Cell>
          <Table.Cell className="release-candidate-file-quality">
            {fileQuality(file)}
          </Table.Cell>
        </Table.Row>
      ))}
    </Table.Body>
  </Table>
);

const ReleaseCandidate = ({
  candidate,
  disabled,
  loadingFolder,
  onLoadFolder,
  release,
  searchId,
}) => {
  const [expanded, setExpanded] = useState(false);
  const [includeExtras, setIncludeExtras] = useState(true);
  const [downloading, setDownloading] = useState(false);
  const [queued, setQueued] = useState(false);

  const { complete, extras, matches, other, user } = candidate;

  const plan = downloadPlan({ candidate, includeExtras, release });
  const fileCount = plan.reduce(
    (total, batch) => total + batch.files.length,
    0,
  );
  const planSize = plan.reduce(
    (total, batch) =>
      total + batch.files.reduce((sum, file) => sum + (file.size ?? 0), 0),
    0,
  );

  const download = async () => {
    setDownloading(true);

    try {
      let failed = 0;

      for (const batch of plan) {
        const response = await enqueueBatch({
          files: batch.files,
          options: { destination: batch.destination },
          searchId,
          username: user.username,
        });

        failed += response?.data?.failures?.length ?? 0;
      }

      if (failed > 0) {
        toast.warning(
          `Queued ${fileCount - failed} of ${fileCount} files from ${user.username}; ${failed} failed`,
        );
      } else {
        toast.success(
          `Queued ${fileCount} files from ${user.username} into "${plan[0].destination.split('/')[0]}"`,
        );
      }

      setQueued(true);
    } catch (error) {
      toast.error(`Could not queue the download: ${errorText(error)}`);
    } finally {
      setDownloading(false);
    }
  };

  return (
    <Segment
      className={`release-candidate ${complete ? 'complete' : 'partial'}`}
      raised
    >
      <CandidateSummary candidate={candidate} />
      <div className="release-candidate-actions">
        <Button
          color={queued ? undefined : 'green'}
          content={queued ? 'Queued' : 'Download'}
          disabled={disabled || downloading || fileCount === 0}
          icon={queued ? 'check' : 'download'}
          label={{
            as: 'a',
            basic: false,
            content: `${fileCount} file${fileCount === 1 ? '' : 's'}, ${formatBytes(planSize)}`,
          }}
          labelPosition="right"
          loading={downloading}
          onClick={download}
          size="small"
        />
        {other.length > 0 && (
          <Checkbox
            checked={includeExtras}
            label={`Include ${other.length} other file${other.length === 1 ? '' : 's'} (cover art, logs)`}
            onChange={() => setIncludeExtras(!includeExtras)}
          />
        )}
        <span className="release-candidate-buttons">
          {!complete && (
            <Button
              content="Load whole folder"
              disabled={disabled || loadingFolder}
              icon="folder open outline"
              loading={loadingFolder}
              onClick={onLoadFolder}
              size="small"
              title="Search results only include files that match the search; list the folder to find the rest"
            />
          )}
          <Button
            content={expanded ? 'Hide tracks' : 'Show tracks'}
            icon={expanded ? 'caret up' : 'caret down'}
            onClick={() => setExpanded(!expanded)}
            size="small"
          />
        </span>
      </div>
      {expanded && (
        <CandidateTracks
          extras={extras}
          matches={matches}
        />
      )}
    </Segment>
  );
};

export default ReleaseCandidate;
