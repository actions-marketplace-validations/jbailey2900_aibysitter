"""Prints a GitHub ::error annotation for each failed test in TestResults/**/*.trx."""

import glob
import xml.etree.ElementTree as ET

ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}

def esc_data(value):
    return value.replace("%", "%25").replace("\r", "%0D").replace("\n", "%0A")

def esc_prop(value):
    return esc_data(value).replace(":", "%3A").replace(",", "%2C")

for path in glob.glob("TestResults/**/*.trx", recursive=True):
    for result in ET.parse(path).getroot().iterfind(".//t:UnitTestResult[@outcome='Failed']", ns):
        name = result.get("testName", "unknown test")
        message = result.findtext("t:Output/t:ErrorInfo/t:Message", "", ns).strip()
        stack = result.findtext("t:Output/t:ErrorInfo/t:StackTrace", "", ns).strip()
        stack = "\n".join(stack.splitlines()[:15])
        body = (message + "\n\n" + stack)[:4000]
        print(f"::error title={esc_prop('Failed: ' + name)}::{esc_data(body)}")
