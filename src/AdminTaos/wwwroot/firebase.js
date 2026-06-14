// ES module — loaded via <script type="module" src="firebase.js"></script>
// Exposes a global window.taos namespace of helpers callable from Blazor IJSRuntime.

import { initializeApp } from "https://www.gstatic.com/firebasejs/10.13.0/firebase-app.js";
import {
  getAuth, onAuthStateChanged,
  signInWithEmailAndPassword, createUserWithEmailAndPassword, signOut
} from "https://www.gstatic.com/firebasejs/10.13.0/firebase-auth.js";
import {
  getFirestore, doc, getDoc, setDoc, updateDoc, deleteDoc,
  collection, getDocs, query, where, Timestamp
} from "https://www.gstatic.com/firebasejs/10.13.0/firebase-firestore.js";
import {
  getStorage, ref as storageRef, uploadBytes, getDownloadURL
} from "https://www.gstatic.com/firebasejs/10.13.0/firebase-storage.js";

import { firebaseConfig } from "./firebase-config.js";

const app  = initializeApp(firebaseConfig);
const auth = getAuth(app);
const db   = getFirestore(app);
const st   = getStorage(app);

// --- helpers: JSON-safe conversions ---
// Firestore Timestamps must be converted to ISO strings for Blazor JSON marshalling,
// and back to Timestamps for writes. DateOnly/TimeOnly are stored as plain strings.
function fromFirestore(v) {
  if (v === null || v === undefined) return null;
  if (v instanceof Timestamp) return v.toDate().toISOString();
  if (Array.isArray(v)) return v.map(fromFirestore);
  if (typeof v === "object") {
    const o = {};
    for (const k of Object.keys(v)) o[k] = fromFirestore(v[k]);
    return o;
  }
  return v;
}
function toFirestore(v, key) {
  if (v === null || v === undefined) return null;
  // Field-name-driven Timestamp conversion: any field whose camelCase name ends with `At`
  // (e.g. createdAt, startedAt, sentAt, managerAdjustedStart) carrying an ISO string is a Timestamp.
  const isTimestampField = (k) => typeof k === "string"
    && (k.endsWith("At") || k.endsWith("Start") || k.endsWith("End") || k === "managerAdjustedStart" || k === "managerAdjustedEnd");
  if (isTimestampField(key) && typeof v === "string") {
    const d = new Date(v); if (!isNaN(d.getTime())) return Timestamp.fromDate(d);
  }
  if (Array.isArray(v)) return v.map((x) => toFirestore(x, null));
  if (typeof v === "object") {
    const o = {};
    for (const k of Object.keys(v)) o[k] = toFirestore(v[k], k);
    return o;
  }
  return v;
}

// --- Auth ---
async function login(email, password) {
  try { const c = await signInWithEmailAndPassword(auth, email, password); return c.user.uid; }
  catch (e) { return null; }
}
async function register(email, password) {
  try { const c = await createUserWithEmailAndPassword(auth, email, password); return c.user.uid; }
  catch (e) { return null; }
}
async function logout()         { await signOut(auth); }
function currentUid()           { return auth.currentUser ? auth.currentUser.uid : null; }
function watchAuth(dotnetRef) {
  onAuthStateChanged(auth, (u) => dotnetRef.invokeMethodAsync("OnAuthChangedFromJs", u ? u.uid : null));
}

// --- Firestore ---
async function getAll(col) {
  const snap = await getDocs(collection(db, col));
  return snap.docs.map(d => ({ id: d.id, ...fromFirestore(d.data()) }));
}
async function getOne(col, id) {
  const snap = await getDoc(doc(db, col, id));
  return snap.exists() ? ({ id: snap.id, ...fromFirestore(snap.data()) }) : null;
}
async function queryByField(col, field, value) {
  const q = query(collection(db, col), where(field, "==", value));
  const snap = await getDocs(q);
  return snap.docs.map(d => ({ id: d.id, ...fromFirestore(d.data()) }));
}
async function setOne(col, id, data) {
  const { id: _ignore, ...rest } = data; // never store `id` field inside the doc
  await setDoc(doc(db, col, id), toFirestore(rest, null));
}
async function updateOne(col, id, data) {
  const { id: _ignore, ...rest } = data;
  await updateDoc(doc(db, col, id), toFirestore(rest, null));
}
async function deleteOne(col, id) { await deleteDoc(doc(db, col, id)); }

// --- Storage (avatar upload + compression in JS) ---
async function pickAndUploadAvatar(uid) {
  return new Promise((resolve, reject) => {
    const input = document.createElement("input");
    input.type = "file";
    input.accept = "image/*";
    input.style.display = "none";
    document.body.appendChild(input);

    let settled = false;
    const cleanup = () => {
      if (document.body.contains(input)) document.body.removeChild(input);
    };
    const settle = (fn) => (...args) => {
      if (settled) return;
      settled = true;
      cleanup();
      fn(...args);
    };
    const resolveOnce = settle(resolve);
    const rejectOnce  = settle(reject);

    input.addEventListener("change", async () => {
      try {
        const file = input.files && input.files[0];
        if (!file) { resolveOnce(null); return; }
        if (file.size > 2 * 1024 * 1024) { rejectOnce(new Error("Fichier > 2 Mo")); return; }
        if (!file.type.startsWith("image/")) { rejectOnce(new Error("Pas une image")); return; }
        const blob = await compressToJpeg(file, 512, 0.85);
        const r = storageRef(st, `avatars/${uid}.jpg`);
        await uploadBytes(r, blob, { contentType: "image/jpeg" });
        const url = await getDownloadURL(r);
        resolveOnce(url);
      } catch (e) { rejectOnce(e); }
    }, { once: true });

    // Cancel event (Chrome 113+, Edge 113+, Firefox 91+)
    input.addEventListener("cancel", () => resolveOnce(null), { once: true });

    // Focus fallback (Safari + older browsers without 'cancel'): when focus returns to the window,
    // wait briefly then check whether a file was selected. If not, treat as cancellation.
    const focusFallback = () => setTimeout(() => {
      if (!settled && (!input.files || input.files.length === 0)) {
        resolveOnce(null);
      }
    }, 300);
    window.addEventListener("focus", focusFallback, { once: true });

    input.click();
  });
}
function compressToJpeg(file, max, q) {
  return new Promise((resolve, reject) => {
    const img = new Image();
    const r = new FileReader();
    r.onerror = reject;
    r.onload = () => { img.src = r.result; };
    img.onerror = reject;
    img.onload = () => {
      const side = Math.min(img.naturalWidth, img.naturalHeight);
      const sx = (img.naturalWidth - side) / 2;
      const sy = (img.naturalHeight - side) / 2;
      const c = document.createElement("canvas");
      c.width = max; c.height = max;
      const ctx = c.getContext("2d");
      ctx.drawImage(img, sx, sy, side, side, 0, 0, max, max);
      c.toBlob((b) => b ? resolve(b) : reject(new Error("toBlob failed")), "image/jpeg", q);
    };
    r.readAsDataURL(file);
  });
}

// --- Expose to Blazor ---
window.taos = {
  login, register, logout, currentUid, watchAuth,
  getAll, getOne, queryByField, setOne, updateOne, deleteOne,
  pickAndUploadAvatar
};
