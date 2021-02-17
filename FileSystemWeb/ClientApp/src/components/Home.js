import React, {useEffect, useState} from 'react';
import {normalizeFolder, normalizeFile} from '../Helpers/Path';
import FolderViewer from './FolderViewer';
import FileViewerOverlay from './FileViewerOverlay';
import {generateQueryUrl} from '../Helpers/generateNavigationUrl';

function setDocumentTitle(folderContent, fileInfo) {
    let name = null;
    if (fileInfo && fileInfo.name) {
        name = fileInfo.name;
    } else if (folderContent && folderContent.path) {
        name = folderContent.path.length ? folderContent.path[folderContent.path.length - 1].name : 'Root';
    }

    document.title = name ? `${name} - File System` : 'File System';
}

export default function () {
    const query = new URLSearchParams(window.location.search);
    const folder = query.get('folder');
    const file = query.get('file');
    const folderNorm = folder && normalizeFolder(folder) || '';
    const fileNameDecoded = file && normalizeFile(file);
    const fileNorm = fileNameDecoded && (folderNorm + fileNameDecoded);
    const closeFileOverlayUrl = generateQueryUrl({file: null});

    const [folderContent, setFolderContent] = useState(null);
    const [fileInfo, setFileInfo] = useState(null);

    let previousFile = null;
    let nextFile = null;
    if (fileNameDecoded && folderContent && folderContent.files && folderContent.files.length) {
        const currentFileIndex = folderContent.files.findIndex(file => file.name === fileNameDecoded);
        console.log('current index:', currentFileIndex);
        if (currentFileIndex !== -1) {
            const size = folderContent.files.length;
            previousFile = folderContent.files[(currentFileIndex - 1 + size) % size];
            nextFile = folderContent.files[(currentFileIndex + 1) % size];
        }
    }

    useEffect(() => {
        setDocumentTitle(folderContent, fileInfo);
    }, [folderContent, fileInfo])

    return (
        <div>
            <FolderViewer path={folderNorm} onFolderLoaded={setFolderContent}/>
            {fileNorm && (
                <FileViewerOverlay path={fileNorm} closeUrl={closeFileOverlayUrl}
                                   previousItem={previousFile && ({
                                       url: generateQueryUrl({file: previousFile.name}),
                                       title: previousFile.name,
                                   })}
                                   nextItem={nextFile && ({
                                       url: generateQueryUrl({file: nextFile.name}),
                                       title: nextFile.name,
                                   })}
                                   onFileInfoLoaded={setFileInfo}/>
            )}
        </div>
    );
}
