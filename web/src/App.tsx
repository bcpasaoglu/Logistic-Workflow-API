import { useCallback, useEffect, useState } from 'react';
import { api, setUnauthorizedHandler, tokenStore } from './api';
import { FleetPanel } from './components/FleetPanel';
import { Login } from './components/Login';
import { NewShipmentForm } from './components/NewShipmentForm';
import { ShipmentDetail } from './components/ShipmentDetail';
import { ShipmentsPanel } from './components/ShipmentsPanel';
import { loadByVehicle } from './lib/fleet';
import type { CurrentUser, PagedResult, Shipment, ShipmentStatus, Vehicle } from './types';

const PAGE_SIZE = 5;

export default function App() {
  const [signedIn, setSignedIn] = useState(() => tokenStore.get() != null);
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [vehicles, setVehicles] = useState<Vehicle[]>([]);
  const [inTransit, setInTransit] = useState<Shipment[]>([]);
  const [shipments, setShipments] = useState<PagedResult<Shipment> | null>(null);
  const [filter, setFilter] = useState<ShipmentStatus | undefined>(undefined);
  const [pageNumber, setPageNumber] = useState(1);
  const [selected, setSelected] = useState<Shipment | null>(null);
  const [healthy, setHealthy] = useState<boolean | null>(null);

  const signOut = useCallback(() => {
    tokenStore.clear();
    setSignedIn(false);
    setUser(null);
    setSelected(null);
  }, []);

  useEffect(() => setUnauthorizedHandler(signOut), [signOut]);

  // Fleet data plus the in-transit shipments used to show each vehicle's load.
  const refreshFleet = useCallback(async () => {
    const [v, t] = await Promise.all([api.vehicles(), api.shipments(1, 100, 'InTransit')]);
    setVehicles(v.items);
    setInTransit(t.items);
  }, []);

  const refreshShipments = useCallback(async () => {
    const page = await api.shipments(pageNumber, PAGE_SIZE, filter);
    setShipments(page);
  }, [pageNumber, filter]);

  useEffect(() => {
    if (!signedIn) return;
    api.me().then(setUser).catch(() => {});
    refreshFleet().catch(() => {});
  }, [signedIn, refreshFleet]);

  useEffect(() => {
    if (signedIn) refreshShipments().catch(() => {});
  }, [signedIn, refreshShipments]);

  useEffect(() => {
    fetch('/health/ready')
      .then((r) => setHealthy(r.ok))
      .catch(() => setHealthy(false));
  }, [signedIn]);

  const refreshAll = useCallback(
    async (updated?: Shipment) => {
      if (updated) setSelected(updated);
      await Promise.all([refreshFleet(), refreshShipments()]);
    },
    [refreshFleet, refreshShipments],
  );

  if (!signedIn) return <Login onSignedIn={() => setSignedIn(true)} />;

  return (
    <div className="app">
      <header className="topbar">
        <div className="brand">
          <span className="logo" aria-hidden>
            LD
          </span>
          <span>LogisticDesk</span>
        </div>
        <div className="topbar-right">
          <span className={`health ${healthy ? 'ok' : healthy === false ? 'down' : ''}`}>
            <span className="dot" />
            {healthy ? 'API online' : healthy === false ? 'API unreachable' : 'Checking API'}
          </span>
          {user && (
            <span className="user">
              <span className="avatar" aria-hidden>
                {(user.firstName[0] ?? '') + (user.lastName[0] ?? '')}
              </span>
              {user.firstName} {user.lastName}
            </span>
          )}
          <button className="btn btn-ghost btn-sm" onClick={signOut}>
            Sign out
          </button>
        </div>
      </header>

      <main className="grid">
        <FleetPanel vehicles={vehicles} load={loadByVehicle(inTransit)} onChanged={() => refreshAll()} />

        <div className="column">
          <ShipmentDetail
            shipment={selected}
            vehicles={vehicles}
            onChanged={refreshAll}
            onDeleted={() => {
              setSelected(null);
              refreshAll();
            }}
          />
          <NewShipmentForm
            vehicles={vehicles}
            onCreated={(s) => {
              setSelected(s);
              setFilter(undefined);
              setPageNumber(1);
              refreshAll();
            }}
          />
        </div>

        <ShipmentsPanel
          page={shipments}
          filter={filter}
          selectedId={selected?.id ?? null}
          onFilter={(f) => {
            setFilter(f);
            setPageNumber(1);
          }}
          onPage={setPageNumber}
          onSelect={setSelected}
        />
      </main>
    </div>
  );
}
