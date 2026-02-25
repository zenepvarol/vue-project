# Live Flight Simulation

An interactive aviation interface built with Vue and Leaflet that visualizes flight movements, routes, and emergency scenarios in real time.

The application renders aircraft motion smoothly on a map, allows user interaction, and simulates operational flight scenarios such as emergency landings and return-to-base navigation.

Repository hosted on GitHub.

---

## Features

### Dynamic Flight Simulation

* Time-step based flight animation
* Smooth marker movement
* Heading-based aircraft rotation
* Real-time route progression visualization

### Emergency Scenarios

* Nearest airport detection algorithm
* Glide-style emergency landing simulation
* Return-to-base navigation logic
* Automatic system reset after mission completion (Fresh Start)

### Route Visualization

* Full flight path rendering (static route)
* Active route progression (dynamic line)
* Departure and arrival markers
* Target direction guide line (dashed path)

### Interactive Controls

* Flight selection from list or map
* Pause / resume simulation
* Sidebar toggle
* Dark / light theme switch
* Callsign search filter

---

## Tech Stack

* **Framework:** Vue 3 (Composition API)
* **Map Engine:** Leaflet
* **Map Tiles:** OpenStreetMap
* **Animation:** Smooth marker slide + rotation
* **Architecture:** Modular CSS structure
* **Data Model:** ICAO-based grouped flight time series

---

## System Architecture

The application is built around three main layers:

### 1. Flight Data Layer

* ICAO-grouped time series
* Animation step management

### 2. Map Layer

* Marker management
* Route rendering
* Terminal markers

### 3. Simulation Engine

* Step-based animation
* Scenario control
* Pause / resume logic
* Route clearing and reset mechanism

---

## Interface Structure

The application uses a dual sidebar layout.

### Left Panel

* Flight list
* Search filter
* Flight selection

### Right Panel

* Selected aircraft telemetry
* Speed
* Altitude
* Distance traveled
* Route completion progress
* Simulation controls

---

## 🔧 Installation

### Requirements

* Node.js (recommended v14 or higher)
* npm or yarn
* Git

---

### 1. Clone the repository

```bash
git clone https://github.com/zenepvarol/vue-project.git
cd vue-project
```

---

### 2. Install dependencies

```bash
npm install
# or
yarn
```

---

### 3. Start development server

```bash
npm run dev
# or
yarn dev
```

The app will typically run at:

```
http://localhost:5173
```

---

### 4. Build for production

```bash
npm run build
# or
yarn build
```

---

### 5. Preview production build (optional)

```bash
npm run preview
# or
yarn preview
```

---

## Important Files

* `public/eskisi.json` → simulation flight dataset
* `flight-style.css` → modular styling
* Leaflet plugin imports must be included in main entry file:

```js
import 'leaflet/dist/leaflet.css'
import 'leaflet-rotatedmarker'
import 'leaflet.marker.slideto'
```

If installing marker slide plugin manually:

```bash
npm install leaflet.marker.slideto
```

---

## Notes

* Use `slideTo()` instead of `setLatLng()` for smooth movement.
* For realistic motion, calculate animation duration from time differences.
* Ensure Leaflet CSS is properly imported.
* Keep map tile attribution intact.

---

## Development Areas

* Multi-flight simultaneous scenario handling
* Speed multiplier control (1x / 5x / 10x)
* Timeline control interface
* Advanced emergency simulations
* Extended telemetry data

---

## Project Purpose

This project provides an experimental and visual environment for:

* flight data visualization
* route simulation modeling
* interactive mapping systems
* aviation scenario prototyping

---

## Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Open a Pull Request

Please follow conventional commit messages (`feat:`, `fix:`, etc.).

---

## License

License information to be added.
