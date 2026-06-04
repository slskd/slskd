import * as sharesLibrary from '../../../lib/shares';
import * as transfersLibrary from '../../../lib/transfers';
import { LoaderSegment, ShrinkableButton, Switch } from '../../Shared';
import ContentsModal from './ContentsModal';
import ExclusionTable from './ExclusionTable';
import ShareTable from './ShareTable';
import TrafficStatsTable from './TrafficStatsTable';
import React, { useEffect, useState } from 'react';
import { toast } from 'react-toastify';
import { Divider } from 'semantic-ui-react';

const ScanButton = ({ rescan, scanPending, working }) => (
  <ShrinkableButton
    color={scanPending ? 'yellow' : undefined}
    disabled={working}
    icon="refresh"
    loading={working}
    mediaQuery="(max-width: 516px)"
    onClick={() => rescan()}
    primary={!scanPending}
  >
    Rescan Shares
  </ShrinkableButton>
);

const CancelButton = ({ cancel, working }) => (
  <ShrinkableButton
    color="red"
    disabled={working}
    icon="x"
    mediaQuery="(max-width: 516px)"
    onClick={() => cancel()}
  >
    Cancel Scan
  </ShrinkableButton>
);

const Shares = ({ state = {}, theme } = {}) => {
  const [loading, setLoading] = useState(true);
  const [working, setWorking] = useState(false);
  const [shares, setShares] = useState([]);
  const [modal, setModal] = useState(false);
  const [trafficStats, setTrafficStats] = useState({});

  const { directories, files, scanPending, scanProgress, scanning } = state;

  const getAll = async (quiet = false) => {
    try {
      if (!quiet) setLoading(true);

      const sharesByHost = await sharesLibrary.getAll();
      const flattened = Object.entries(sharesByHost).reduce(
        (accumulator, [host, sharesForHost]) => {
          return accumulator.concat(
            sharesForHost.map((share) => ({ host, ...share })),
          );
        },
        [],
      );

      setShares(flattened);
    } catch (error) {
      console.error(error);
      toast.error(error?.response?.data ?? error?.message ?? error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    getAll();
  }, []);

  useEffect(() => {
    getAll(true);

    if (!scanning) {
      // the state change out of scanning can fire before
      // shares are updated, which leaves them stale. wait a second
      // and fetch again.
      setTimeout(() => getAll(true), 1_000);
    }
  }, [scanPending, scanning]);

  useEffect(() => {
    const bytesTransferred = (statuses) =>
      Object.values(statuses).reduce(
        (accumulator, current) => current.totalBytes + accumulator,
        0,
      );
    const fetchStats = async () => {
      const statsResponse = await transfersLibrary.getStats();
      const stats = statsResponse.data;
      const uploadedFiles = stats.Upload?.Succeeded?.count ?? 0;
      const downloadedFiles = stats.Download?.Succeeded?.count ?? 0;
      const uploadedBytes = bytesTransferred(stats.Upload);
      const downloadedBytes = bytesTransferred(stats.Download);
      setTrafficStats({
        uploadedBytes,
        uploadedFiles,
        downloadedBytes,
        downloadedFiles,
      });
    };

    fetchStats();
  }, []);

  const rescan = async () => {
    try {
      setWorking(true);
      await sharesLibrary.rescan();
    } catch (error) {
      console.error(error);
      toast.error(error?.response?.data ?? error?.message ?? error);
    } finally {
      setWorking(false);
    }
  };

  const cancel = async () => {
    try {
      setWorking(true);
      await sharesLibrary.cancel();
    } catch (error) {
      console.error(error);
      toast.error(
        error?.response?.data ??
          error?.message ??
          error ??
          'Failed to cancel the scan',
      );
    } finally {
      setWorking(false);
    }
  };

  const shared = shares.filter((share) => !share.isExcluded);
  const excluded = shares.filter((share) => share.isExcluded);

  return (
    <Switch loading={loading && <LoaderSegment />}>
      <div className="header-buttons">
        <Switch
          scanning={
            scanning && (
              <CancelButton
                cancel={cancel}
                working={working}
              />
            )
          }
        >
          <ScanButton
            rescan={rescan}
            scanPending={scanPending}
            working={working}
          />
        </Switch>
      </div>
      <Divider />
      <Switch
        filling={
          scanning && (
            <LoaderSegment>
              <div>
                <div>{Math.round(scanProgress * 100)}%</div>
                <div className="share-scan-detail">
                  Found {files} files in {directories} directories
                </div>
              </div>
            </LoaderSegment>
          )
        }
      >
        <ShareTable
          onClick={setModal}
          shares={shared}
        />
        <ExclusionTable exclusions={excluded} />
        <TrafficStatsTable stats={trafficStats} />
      </Switch>
      <ContentsModal
        onClose={() => setModal(false)}
        share={modal}
        theme={theme}
      />
    </Switch>
  );
};

export default Shares;
