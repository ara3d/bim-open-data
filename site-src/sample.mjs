// The bundled samples: openly licensed buildings converted in ../samples/public/ (see its
// NOTICE.md). The build copies each file from there; the page shows each one's credit.
const ccBy = '<a href="https://creativecommons.org/licenses/by/4.0/">CC BY 4.0</a>';
const notice = 'https://github.com/ara3d/bim-open-data/blob/main/samples/public/NOTICE.md';
const digitalHub = `DigitalHub, © 2020 RWTH Aachen University, E3D, from <a href="https://github.com/RWTH-E3D/DigitalHub">RWTH-E3D/DigitalHub</a>, MIT licensed (<a href="${notice}">notice</a>)`;

export const SAMPLES = [
  {
    file: 'schependomlaan.bos',
    title: 'Schependomlaan, design model',
    credit: `Schependomlaan dataset, (C) original owners, from <a href="https://github.com/openBIMstandards/Archive-DataSetSchependomlaan">openBIMstandards</a>, licensed ${ccBy}`,
  },
  { file: 'digitalhub-arc.bos', title: 'DigitalHub, architecture', credit: digitalHub },
  { file: 'digitalhub-hzg.bos', title: 'DigitalHub, heating', credit: digitalHub },
  {
    file: 'duplex.bos',
    title: 'Duplex Apartment',
    credit: `BSI (2020) Duplex Apartment Test Files, buildingSMART International, from <a href="https://github.com/buildingsmart-community/Community-Sample-Test-Files">Community Sample Test Files</a>, licensed ${ccBy}`,
  },
];

/** Where the build reads the samples from, relative to this folder. */
export const SAMPLE_FOLDER = '../samples/public/';
