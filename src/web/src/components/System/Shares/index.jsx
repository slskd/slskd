import * as sharesLibrary from '../../../lib/shares';
import { LoaderSegment, ShrinkableButton, Switch } from '../../Shared';
import ContentsModal from './ContentsModal';
import ExclusionTable from './ExclusionTable';
import ShareTable from './ShareTable';
import React, { useEffect, useState } from 'react';
import { toast } from 'react-toastify';
import { Divider } from 'semantic-ui-react';

const Index = ({ state = {} } = {}) => {
  const [loading, setLoading] = useState(true);
  const [working, setWorking] = useState(false);
  const [shares, setShares] = useState([]);
  const [modal, setModal] = useState(false);

  const { directories, files, scanPending, scanProgress, scanning } = state;

  useEffect(() => {
    getAll();
  }, []);

  useEffect(() => {
    getAll({ quiet: true });

    if (!scanning) {
      // the state change out of scanning can fire before
      // shares are updated, which leaves them stale. wait a second
      // and fetch again.
      setTimeout(() => getAll({ quiet: true }), 1_000);
    }
  }, [scanPending, scanning]);

  const getAll = async ({ quiet } = { quiet: false }) => {
    try {
      if (!quiet) setLoading(true);

      const sharesByHost = await sharesLibrary.getAll();
      const flattened = Object.entries(sharesByHost).reduce(
        (accumulator, [host, shares]) => {
          return accumulator.concat(
            shares.map((share) => ({ host, ...share })),
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

  const ScanButton = () => (
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

  const CancelButton = () => (
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

  const shared = shares.filter((share) => !share.isExcluded);
  const excluded = shares.filter((share) => share.isExcluded);

  return (
    <Switch loading={loading && <LoaderSegment />}>
      <div className="header-buttons">
        <Switch scanning={scanning && <CancelButton />}>
          <ScanButton />
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
      </Switch>
      <ContentsModal
        onClose={() => setModal(false)}
        share={modal}
      />
    </Switch>
  );
};

export default Index;
