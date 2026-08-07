#!/usr/bin/env python3
"""Summarize the newest Unity EditMode results XML: totals + every failure.

Usage: python3 tools/report_test_results.py [results.xml | log-dir]
Default: _workspace/current/engineering/unity-logs
"""
import glob
import os
import sys
import xml.etree.ElementTree as ET

target = sys.argv[1] if len(sys.argv) > 1 else "_workspace/current/engineering/unity-logs"
if os.path.isdir(target):
    candidates = sorted(glob.glob(os.path.join(target, "test-results-*.xml")),
                        key=os.path.getmtime)
    if not candidates:
        sys.exit("no test-results-*.xml under " + target)
    target = candidates[-1]

root = ET.parse(target).getroot()
print(os.path.basename(target),
      "total=%s passed=%s failed=%s result=%s" % (
          root.attrib.get("total"), root.attrib.get("passed"),
          root.attrib.get("failed"), root.attrib.get("result")))
for case in root.iter("test-case"):
    if case.attrib.get("result") == "Failed":
        print("FAIL:", case.attrib["fullname"])
        message = case.find("failure/message")
        if message is not None:
            print("     ", (message.text or "")[:500].replace("\n", " | "))
